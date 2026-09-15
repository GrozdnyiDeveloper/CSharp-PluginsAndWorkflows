using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ExamplePlugins
{
    /// <summary>
    /// Пример плагина, запускающийся на разные триггеры
    /// И регистрируемый на разные сущности
    /// Для синхронизации данных записей сущностей Тендер и Запрос и дочрених к ним записей Продуктов и Примечаний
    /// </summary>
    public class SynchronizeTenderAndRequest : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            IOrganizationServiceFactory serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity)
            {
                string entityName = ((Entity)context.InputParameters["Target"]).LogicalName;
                try
                {
                    switch(entityName)
                    {
                        case "edu_tender":
                            tracingService.Trace("Вход в edu_tender");
                            Entity tender = (Entity)context.InputParameters["Target"];
                            if (tender.GetAttributeValue<EntityReference>("edu_requestid") != null)
                            {
                                EntityReference requestRef = tender.GetAttributeValue<EntityReference>("edu_requestid");
                                Entity request = service.Retrieve(requestRef.LogicalName, requestRef.Id, new ColumnSet(allColumns: true));

                                Entity tenderUpdate = new Entity("edu_tender");
                                tenderUpdate.Id = tender.Id;
                                tenderUpdate["edu_synchronize_process"] = true;
                                service.Update(tenderUpdate);

                                SynchronizeAnnotations(service, tender, request);

                                SynchronizeProducts(service, tender, request);

                                tenderUpdate["edu_synchronize_process"] = false;
                                service.Update(tenderUpdate);
                            }
                            break;

                        case "annotation":
                            {
                                Entity annotation = (Entity)context.InputParameters["Target"];

                                QueryExpression query = new QueryExpression("edu_tender");
                                query.TopCount = 1;
                                query.ColumnSet = new ColumnSet(allColumns: true);
                                query.NoLock = true;
                                query.Criteria.AddCondition("edu_tenderid", ConditionOperator.Equal, annotation.GetAttributeValue<EntityReference>("objectid").Id);
                                Entity tenderEntity = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                                query = new QueryExpression("edu_request");
                                query.TopCount = 1;
                                query.ColumnSet = new ColumnSet(allColumns: true);
                                query.NoLock = true;
                                query.Criteria.AddCondition("edu_requestid", ConditionOperator.Equal, annotation.GetAttributeValue<EntityReference>("objectid").Id);
                                Entity requestEntity = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                                switch (context.MessageName)
                                {
                                    case "Create":
                                        if (tenderEntity != null)
                                        {
                                            var requestRef = CheckForBindingAnnotation(service, annotation, "edu_requestid", "edu_tenderid");
                                            if (requestRef != null)
                                            {
                                                requestEntity = service.Retrieve(((EntityReference)requestRef).LogicalName, ((EntityReference)requestRef).Id, new ColumnSet(allColumns: true));
                                            }
                                        }
                                        else
                                        {
                                            var tenderId = CheckForBindingAnnotation(service, annotation, "edu_tenderid", "edu_requestid");
                                            if (tenderId != null)
                                            {
                                                tenderEntity = service.Retrieve("edu_tender", (Guid)tenderId, new ColumnSet(allColumns: true));
                                            }
                                        }
                                        if (!tenderEntity.GetAttributeValue<bool>("edu_synchronize_process"))
                                        {
                                            SynchronizeAnnotations(service, tenderEntity, requestEntity);
                                        }
                                        break;

                                    case "Update":
                                        if (context.PreEntityImages.Contains("Image"))
                                        {
                                            Entity preImageAnnotation = context.PreEntityImages["Image"];

                                            Entity bindedAnnotation = new Entity("annotation");
                                            if (tenderEntity != null)
                                            {
                                                var requestRef = CheckForBindingAnnotation(service, annotation, "edu_requestid", "edu_tenderid");
                                                if (requestRef != null)
                                                {
                                                    bindedAnnotation = GetBindedAnotation(service, (EntityReference)requestRef, preImageAnnotation);
                                                }
                                            }
                                            else
                                            {
                                                var tenderId = CheckForBindingAnnotation(service, annotation, "edu_tenderid", "edu_requestid");
                                                if (tenderId != null)
                                                {
                                                    bindedAnnotation = GetBindedAnotation(service, new EntityReference("edu_tender", (Guid)tenderId), preImageAnnotation);
                                                }
                                            }
                                            if (bindedAnnotation != null)
                                            {
                                                Entity updateAnnotation = new Entity("annotation");
                                                updateAnnotation.Id = bindedAnnotation.Id;
                                                updateAnnotation.Attributes["annotationid"] = bindedAnnotation.GetAttributeValue<Guid>("annotationid");
                                                updateAnnotation.Attributes["subject"] = annotation.GetAttributeValue<object>("subject") ?? "";
                                                updateAnnotation.Attributes["notetext"] = annotation.GetAttributeValue<object>("notetext") ?? "";
                                                updateAnnotation.Attributes["documentbody"] = annotation.GetAttributeValue<object>("documentbody") ?? "";
                                                updateAnnotation.Attributes["filename"] = annotation.GetAttributeValue<object>("filename") ?? "";
                                                service.Update(updateAnnotation);
                                            }
                                        }
                                        break;

                                    case "Delete":
                                        if (context.PreEntityImages.Contains("Image"))
                                        {
                                            Entity preImageAnnotation = context.PreEntityImages["Image"];
                                            if (tenderEntity != null)
                                            {
                                                var requestRef = CheckForBindingAnnotation(service, preImageAnnotation, "edu_requestid", "edu_tenderid");
                                                if (requestRef != null)
                                                {
                                                    Entity requestAnnotation = GetBindedAnotation(service, (EntityReference)requestRef, preImageAnnotation);
                                                    service.Delete(requestAnnotation.LogicalName, requestAnnotation.Id);
                                                }
                                            }
                                            else
                                            {
                                                var tenderId = CheckForBindingAnnotation(service, preImageAnnotation, "edu_tenderid", "edu_requestid");
                                                if (tenderId != null)
                                                {
                                                    Entity tenderAnnotation = GetBindedAnotation(service, new EntityReference("edu_tender", (Guid)tenderId), preImageAnnotation);
                                                    service.Delete("annotation", tenderAnnotation.GetAttributeValue<Guid>("annotationid"));
                                                }
                                            }
                                        }
                                        break;
                                }
                            }
                            break;

                        case "edu_tender_product":
                            {
                                Entity tenderProductEntity = (Entity)context.InputParameters["Target"];

                                QueryExpression query = new QueryExpression("edu_tender");
                                query.TopCount = 1;
                                query.ColumnSet = new ColumnSet(allColumns: true);
                                query.NoLock = true;
                                query.Criteria.AddCondition("edu_tenderid", ConditionOperator.Equal, tenderProductEntity.GetAttributeValue<EntityReference>("edu_tenderid").Id);
                                Entity tenderEntity = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                                if (tenderEntity != null)
                                {
                                    switch (context.MessageName)
                                    {
                                        case "Create":
                                            Guid requestId = tenderEntity.GetAttributeValue<EntityReference>("edu_requestid").Id;
                                            if (requestId != null)
                                            {
                                                Entity requestEntity = service.Retrieve("edu_request", requestId, new ColumnSet(allColumns: true));
                                                if (!tenderEntity.GetAttributeValue<bool>("edu_synchronize_process"))
                                                {
                                                    SynchronizeProducts(service, tenderEntity, requestEntity);
                                                }
                                            }
                                            break;

                                        case "Update":
                                            if (context.PreEntityImages.Contains("Image"))
                                            {
                                                Entity preImageTenderProduct = context.PreEntityImages["Image"];

                                                Guid requestIdUpdate = tenderEntity.GetAttributeValue<EntityReference>("edu_requestid").Id;
                                                if (requestIdUpdate != null)
                                                {
                                                    Entity requestProduct = GetBindedRequestProduct(service, requestIdUpdate, preImageTenderProduct);
                                                    if (requestProduct != null)
                                                    {
                                                        Entity updateRequestProduct = new Entity("edu_requestproduct");
                                                        updateRequestProduct.Id = requestProduct.GetAttributeValue<Guid>("edu_productofrequestid");
                                                        updateRequestProduct.Attributes["edu_productofrequestid"] = requestProduct.GetAttributeValue<object>("edu_productofrequestid");
                                                        updateRequestProduct.Attributes["edu_productid"] = tenderProductEntity.GetAttributeValue<object>("edu_productid");
                                                        service.Update(updateRequestProduct);
                                                    }
                                                }
                                            }
                                            break;

                                        case "Delete":
                                            if (context.PreEntityImages.Contains("Image"))
                                            {
                                                Entity preImageTenderProduct = context.PreEntityImages["Image"];
                                                Guid requestIdDelete = tenderEntity.GetAttributeValue<EntityReference>("edu_requestid").Id;
                                                if (requestIdDelete != null)
                                                {
                                                    Entity requestProduct = GetBindedRequestProduct(service, requestIdDelete, preImageTenderProduct);
                                                    service.Delete("edu_requestproduct", requestProduct.GetAttributeValue<Guid>("edu_productofrequestid"));
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                            break;

                        case "edu_request_product":
                            {
                                Entity requestProductEntity = (Entity)context.InputParameters["Target"];

                                QueryExpression query = new QueryExpression("edu_tender");
                                query.TopCount = 1;
                                query.ColumnSet = new ColumnSet(allColumns: true);
                                query.NoLock = true;
                                query.Criteria.AddCondition("edu_requestid", ConditionOperator.Equal, requestProductEntity.GetAttributeValue<EntityReference>("edu_requestid").Id);
                                Entity tenderEntity = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                                if (tenderEntity != null)
                                {
                                    switch (context.MessageName)
                                    {
                                        case "Create":
                                            Entity requestEntity = service.Retrieve("edu_request", requestProductEntity.GetAttributeValue<EntityReference>("edu_requestid").Id, new ColumnSet(allColumns: true));
                                            if (!tenderEntity.GetAttributeValue<bool>("edu_synchronize_process"))
                                            {
                                                SynchronizeProducts(service, tenderEntity, requestEntity);
                                            }
                                            break;

                                        case "Update":
                                            if (context.PreEntityImages.Contains("Image"))
                                            {
                                                Entity preImageRequestProduct = context.PreEntityImages["Image"];

                                                Guid tenderIdUpdate = tenderEntity.GetAttributeValue<EntityReference>("edu_tenderid").Id;
                                                if (tenderIdUpdate != null)
                                                {
                                                    Entity tenderProduct = GetBindedTenderProduct(service, tenderIdUpdate, preImageRequestProduct);
                                                    if (tenderProduct != null)
                                                    {
                                                        Entity updateTenderProduct = new Entity("edu_tender_product");
                                                        updateTenderProduct.Id = tenderProduct.GetAttributeValue<Guid>("edu_tender_productid");
                                                        updateTenderProduct.Attributes["edu_tender_productid"] = tenderProduct.GetAttributeValue<object>("edu_tender_productid");
                                                        updateTenderProduct.Attributes["edu_productid"] = requestProductEntity.GetAttributeValue<object>("edu_productid");
                                                        service.Update(updateTenderProduct);
                                                    }
                                                }
                                            }
                                            break;

                                        case "Delete":
                                            if (context.PreEntityImages.Contains("Image"))
                                            {
                                                Entity preImageRequestProduct = context.PreEntityImages["Image"];
                                                Guid tenderIdDelete = tenderEntity.GetAttributeValue<EntityReference>("edu_tenderid").Id;
                                                if (tenderIdDelete != null)
                                                {
                                                    Entity tenderProduct = GetBindedTenderProduct(service, tenderIdDelete, preImageRequestProduct);
                                                    service.Delete("edu_tender_product", tenderProduct.GetAttributeValue<Guid>("edu_tender_productid"));
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                            break;
                    }
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException($"An error occurred in MyPlug-in: {ex.Message} \n{ex.Source} \n{ex.Detail} \n{ex.Data}", ex);
                }
                catch (Exception ex)
                {
                    tracingService.Trace("MyPlugin: {0}", ex.ToString());
                    throw;
                }
            }
        }

        static void SynchronizeAnnotations(IOrganizationService service, Entity tender, Entity request)
        {
            QueryExpression queryTenderAnnotations = new QueryExpression("annotation");
            queryTenderAnnotations.ColumnSet = new ColumnSet("subject", "notetext", "documentbody", "filename");
            queryTenderAnnotations.NoLock = true;
            queryTenderAnnotations.Criteria.AddCondition("objectid", ConditionOperator.Equal, tender.Id);
            EntityCollection tenderAnnotationsList = service.RetrieveMultiple(queryTenderAnnotations);
            foreach (var tenderAnnotation in tenderAnnotationsList.Entities)
            {
                tenderAnnotation.Attributes.Remove("annotationid");
                tenderAnnotation.Id = Guid.Empty;
            }

            QueryExpression queryRequestAnnotations = new QueryExpression("annotation");
            queryRequestAnnotations.ColumnSet = new ColumnSet("subject", "notetext", "documentbody", "filename");
            queryRequestAnnotations.NoLock = true;
            queryRequestAnnotations.Criteria.AddCondition("objectid", ConditionOperator.Equal, request.Id);
            EntityCollection requestAnnotationsList = service.RetrieveMultiple(queryRequestAnnotations);
            foreach (var requestAnnotation in requestAnnotationsList.Entities)
            {
                requestAnnotation.Attributes.Remove("annotationid");
                requestAnnotation.Id = Guid.Empty;
            }

            EntityCollection unitedAnnotationsList = new EntityCollection();

            foreach (var tenderAnnotation in tenderAnnotationsList.Entities)
            {
                bool isAlreadyInList = false;
                foreach (var unitedAnnotation in unitedAnnotationsList.Entities)
                {
                    if (CompareAnnotations(tenderAnnotation, unitedAnnotation)) isAlreadyInList = true;
                }
                if (!isAlreadyInList) unitedAnnotationsList.Entities.Add(tenderAnnotation);
            }

            foreach (var requestAnnotation in requestAnnotationsList.Entities)
            {
                bool isAlreadyInList = false;
                foreach (var unitedAnnotation in unitedAnnotationsList.Entities)
                {
                    if (CompareAnnotations(requestAnnotation, unitedAnnotation)) isAlreadyInList = true;
                }
                if (!isAlreadyInList) unitedAnnotationsList.Entities.Add(requestAnnotation);
            }

            foreach (var unitedAnnotation in unitedAnnotationsList.Entities)
            {
                bool isAlreadyInList = false;
                foreach (var tenderAnnotation in tenderAnnotationsList.Entities)
                {
                    if (CompareAnnotations(tenderAnnotation, unitedAnnotation)) isAlreadyInList = true;
                }
                if (!isAlreadyInList)
                {
                    unitedAnnotation.Attributes.Add("objectid", new EntityReference("edu_tender", tender.Id));
                    unitedAnnotation.Attributes.Add("objecttypecode", "edu_tender");
                    service.Create(unitedAnnotation);
                    unitedAnnotation.Attributes.Remove("objectid");
                    unitedAnnotation.Attributes.Remove("objecttypecode");

                }

                isAlreadyInList = false;
                foreach (var requestAnnotation in requestAnnotationsList.Entities)
                {
                    if (CompareAnnotations(requestAnnotation, unitedAnnotation)) isAlreadyInList = true;
                }
                if (!isAlreadyInList)
                {
                    unitedAnnotation.Attributes.Add("objectid", new EntityReference("edu_request", request.Id));
                    unitedAnnotation.Attributes.Add("objecttypecode", "edu_request");
                    service.Create(unitedAnnotation);
                    unitedAnnotation.Attributes.Remove("objectid");
                    unitedAnnotation.Attributes.Remove("objecttypecode");
                }
            }
        }

        static void SynchronizeProducts(IOrganizationService service, Entity tender, Entity request)
        {
            QueryExpression queryTenderProducts = new QueryExpression("edu_tender_product");
            queryTenderProducts.ColumnSet = new ColumnSet("edu_productid", "edu_singlepaymentcost", "edu_monthlyfeecost", "edu_marginvalue");
            queryTenderProducts.NoLock = true;
            queryTenderProducts.Criteria.AddCondition("edu_tenderid", ConditionOperator.Equal, tender.Id);
            EntityCollection tenderProductsList = service.RetrieveMultiple(queryTenderProducts);
            foreach (var tenderProduct in tenderProductsList.Entities)
            {
                tenderProduct.Attributes.Remove("edu_tender_productid");
                tenderProduct.Id = Guid.Empty;
            }

            QueryExpression queryRequestProducts = new QueryExpression("edu_productofrequest");
            queryRequestProducts.ColumnSet = new ColumnSet("edu_productid", "edu_singlepaymentcost", "edu_monthlyfeecost", "edu_marginvalue");
            queryRequestProducts.NoLock = true;
            queryRequestProducts.Criteria.AddCondition("edu_requestid", ConditionOperator.Equal, request.Id);
            EntityCollection requestProductsList = service.RetrieveMultiple(queryRequestProducts);
            foreach (var requestProduct in requestProductsList.Entities)
            {
                requestProduct.Attributes.Remove("edu_productofrequestid");
                requestProduct.Id = Guid.Empty;
            }

            EntityCollection unitedProductsList = new EntityCollection();

            foreach (var tenderProduct in tenderProductsList.Entities)
            {
                bool isAlreadyInList = false;
                foreach (var unitedProduct in unitedProductsList.Entities)
                {
                    if (CompareProducts(tenderProduct, unitedProduct)) isAlreadyInList = true;
                }
                if (!isAlreadyInList) unitedProductsList.Entities.Add(tenderProduct);
            }

            foreach (var requestProduct in requestProductsList.Entities)
            {
                bool isAlreadyInList = false;
                foreach (var unitedProduct in unitedProductsList.Entities)
                {
                    if (CompareProducts(requestProduct, unitedProduct)) isAlreadyInList = true;
                }
                if (!isAlreadyInList) unitedProductsList.Entities.Add(requestProduct);
            }

            foreach (var unitedProduct in unitedProductsList.Entities)
            {
                bool isAlreadyInList = false;
                foreach (var tenderProduct in tenderProductsList.Entities)
                {
                    if (CompareProducts(tenderProduct, unitedProduct)) isAlreadyInList = true;
                }
                if (!isAlreadyInList)
                {
                    unitedProduct.Attributes.Add("edu_tenderid", new EntityReference("edu_tender", tender.Id));
                    unitedProduct.LogicalName = "edu_tender_product";
                    service.Create(unitedProduct);
                    unitedProduct.Attributes.Remove("edu_tenderid");
                    unitedProduct.LogicalName = "edu_productofrequest";
                }

                isAlreadyInList = false;
                foreach (var requestProduct in requestProductsList.Entities)
                {
                    if (CompareProducts(requestProduct, unitedProduct)) isAlreadyInList = true;
                }
                if (!isAlreadyInList)
                {
                    unitedProduct.Attributes.Add("edu_requestid", new EntityReference("edu_request", request.Id));
                    unitedProduct.LogicalName = "edu_productofrequest";
                    service.Create(unitedProduct);
                    unitedProduct.Attributes.Remove("edu_requestid");
                    unitedProduct.LogicalName = "edu_tender_product";
                }
            }
        }

        static bool CompareAnnotations(Entity first, Entity second)
        {
            if (object.Equals(first.GetAttributeValue<object>("subject"), second.GetAttributeValue<object>("subject")) && object.Equals(first.GetAttributeValue<object>("notetext"), second.GetAttributeValue<object>("notetext")) &&
                object.Equals(first.GetAttributeValue<object>("documentbody"), second.GetAttributeValue<object>("documentbody")) && object.Equals(first.GetAttributeValue<object>("filename"), second.GetAttributeValue<object>("filename")))
                return true;
            else return false;
        }

        static bool CompareProducts(Entity first, Entity second)
        {
            if (object.Equals(first.GetAttributeValue<object>("edu_productid"), second.GetAttributeValue<object>("edu_productid")) && object.Equals(first.GetAttributeValue<object>("edu_singlepaymentcost"), second.GetAttributeValue<object>("edu_singlepaymentcost")) &&
                object.Equals(first.GetAttributeValue<object>("edu_monthlyfeecost"), second.GetAttributeValue<object>("edu_monthlyfeecost")) && object.Equals(first.GetAttributeValue<object>("edu_marginvalue"), second.GetAttributeValue<object>("edu_marginvalue")))
                return true;
            else return false;
        }

        static dynamic CheckForBindingAnnotation(IOrganizationService service, Entity annotation, string column, string condition)
        {
            QueryExpression query = new QueryExpression("edu_tender");
            query.TopCount = 1;
            query.ColumnSet = new ColumnSet(column);
            query.NoLock = true;
            query.Criteria.AddCondition(condition, ConditionOperator.Equal, ((EntityReference)annotation.Attributes["objectid"]).Id);
            return service.RetrieveMultiple(query).Entities.FirstOrDefault().GetAttributeValue<dynamic>(column);
        }

        static Entity GetBindedAnotation(IOrganizationService service, EntityReference entityRef, Entity preImageAnnotation)
        {
            QueryExpression queryAnnotation = new QueryExpression("annotation");
            queryAnnotation.TopCount = 1;
            queryAnnotation.ColumnSet = new ColumnSet("annotationid", "subject", "notetext", "documentbody", "filename");
            queryAnnotation.NoLock = true;
            queryAnnotation.Criteria.AddCondition("objectid", ConditionOperator.Equal, entityRef.Id);
            queryAnnotation.Criteria.AddCondition("subject", ConditionOperator.Equal, preImageAnnotation.GetAttributeValue<object>("subject"));
            queryAnnotation.Criteria.AddCondition("notetext", ConditionOperator.Equal, preImageAnnotation.GetAttributeValue<object>("notetext"));
            queryAnnotation.Criteria.AddCondition("documentbody", ConditionOperator.Equal, preImageAnnotation.GetAttributeValue<object>("documentbody"));
            queryAnnotation.Criteria.AddCondition("filename", ConditionOperator.Equal, preImageAnnotation.GetAttributeValue<object>("filename"));
            return service.RetrieveMultiple(queryAnnotation).Entities.FirstOrDefault();
        }

        static Entity GetBindedRequestProduct(IOrganizationService service, Guid entityId, Entity preImageProduct)
        {
            QueryExpression queryAnnotation = new QueryExpression("edu_requestproduct");
            queryAnnotation.TopCount = 1;
            queryAnnotation.ColumnSet = new ColumnSet("edu_productid");
            queryAnnotation.NoLock = true;
            queryAnnotation.Criteria.AddCondition("edu_requestid", ConditionOperator.Equal, entityId);
            queryAnnotation.Criteria.AddCondition("edu_productid", ConditionOperator.Equal, preImageProduct.GetAttributeValue<object>("edu_productid"));
            return service.RetrieveMultiple(queryAnnotation).Entities.FirstOrDefault();
        }

        static Entity GetBindedTenderProduct(IOrganizationService service, Guid entityId, Entity preImageProduct)
        {
            QueryExpression queryAnnotation = new QueryExpression("edu_tender_product");
            queryAnnotation.TopCount = 1;
            queryAnnotation.ColumnSet = new ColumnSet("edu_productid");
            queryAnnotation.NoLock = true;
            queryAnnotation.Criteria.AddCondition("edu_tenderid", ConditionOperator.Equal, entityId);
            queryAnnotation.Criteria.AddCondition("edu_productid", ConditionOperator.Equal, preImageProduct.GetAttributeValue<object>("edu_productid"));
            return service.RetrieveMultiple(queryAnnotation).Entities.FirstOrDefault();
        }
    }
}
