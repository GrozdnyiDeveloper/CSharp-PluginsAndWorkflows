using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using System.Windows.Controls;
using System.Collections;
using Newtonsoft.Json;

namespace ExampleWorkflows
{
    public class FormJSONForTender : CodeActivity
    {
        [Input("Тендер")]
        [RequiredArgument]
        [ReferenceTarget("edu_tender")]
        public InArgument<EntityReference> TenderRef { get; set; }

        [Output("JSON сообщение")]
        public OutArgument<string> JSONMessage { get; set; }
        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            var tenderRef = TenderRef.Get(executionContext);

            // Получить данные Тендера и связанных сущностей
            var responce = GetFullTenderInfo(service, tenderRef);

            // Сформировать на их основе JSON документ
            //var autoFormatedJSON = Newtonsoft.Json.JsonConvert.SerializeObject(responce.Entity);
            var formatedJSON = GetFormatedJSON(responce.Entity);

            // Отправить запрос на изменение "Интеграционного сообщения" в Тендере на JSON документ
            var tender = new Entity("edu_tender")
            {
                Id = tenderRef.Id,
            };
            tender["edu_integral_message"] = formatedJSON;
            service.Update(tender);

            JSONMessage.Set(executionContext, formatedJSON);
        }

        // Метод получения информации о Тендере, а также связанных с ним Регионе, Партнёре, Ответственном Партнёра и Продуктах тендера
        protected RetrieveResponse GetFullTenderInfo(IOrganizationService service, EntityReference tenderRef)
        {
            RelationshipQueryCollection relationshipQueryCollection = new RelationshipQueryCollection();

            QueryExpression relatedRegion = new QueryExpression("edu_region")
            {
                ColumnSet = new ColumnSet("edu_name", "edu_regioncodevalue")
            };
            Relationship regionRelationship = new Relationship("edu_edu_region_edu_tender_regionid");
            relationshipQueryCollection.Add(regionRelationship, relatedRegion);

            QueryExpression relatedAccount = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("name", "telephone1")
            };
            Relationship accountRelationship = new Relationship("edu_account_edu_tender_accountid");
            relationshipQueryCollection.Add(accountRelationship, relatedAccount);

            QueryExpression relatedContact = new QueryExpression("contact")
            {
                ColumnSet = new ColumnSet("fullname", "emailaddress1")
            };
            Relationship contactRelationship = new Relationship("edu_contact_edu_tender_contactid");
            relationshipQueryCollection.Add(contactRelationship, relatedContact);

            QueryExpression relatedTenderProducts = new QueryExpression("edu_tender_product")
            {
                ColumnSet = new ColumnSet("edu_productid")
            };
            LinkEntity linkProduct = relatedTenderProducts.AddLink("edu_product", "edu_productid", "edu_productid", JoinOperator.LeftOuter);
            linkProduct.EntityAlias = "product";
            linkProduct.Columns = new ColumnSet("edu_name", "edu_servicelist", "edu_conditionlist", "edu_monthlyfeecost", "edu_singlepaymentcost", "edu_marginvalue");
            Relationship tenderProductRelationship = new Relationship("edu_edu_tender_edu_tender_product_tenderid");
            relationshipQueryCollection.Add(tenderProductRelationship, relatedTenderProducts);

            RetrieveRequest request = new RetrieveRequest()
            {
                ColumnSet = new ColumnSet(allColumns: true),
                Target = tenderRef,
                RelatedEntitiesQuery = relationshipQueryCollection
            };

            return (RetrieveResponse)service.Execute(request);
        }

        // Метод формирования JSON сообщения с данными Тендера
        protected string GetFormatedJSON(Entity entity)
        {
            var tenderValues = new Dictionary<string, object>();
            foreach (var attribute in entity.FormattedValues)
            {
                tenderValues[attribute.Key] = attribute.Value;
            }

            var relatedEntities = new Dictionary<string, object>();
            foreach (var relatedCollection in entity.RelatedEntities)
            {
                var i = 0;
                var j = 0;
                var entitesList = new Dictionary<string, object>();
                string name = relatedCollection.Value.EntityName;
                foreach (var relatedEntity in relatedCollection.Value.Entities)
                {
                    var relatedAttributes = new Dictionary<string, dynamic>();
                    foreach (var attribute in relatedEntity.Attributes)
                    {
                        if (!attribute.Key.Contains("product."))
                        {
                            relatedAttributes[attribute.Key] = attribute.Value;
                        }
                    }

                    var innerRelatedEntities = new Dictionary<string, object>();
                    if (name == "edu_tender_product")
                    {
                        var innerRelatedAttributes = new Dictionary<string, dynamic>();
                        foreach (var attribute in relatedEntity.Attributes)
                        {
                            if (attribute.Key.Contains("product."))
                            {
                                innerRelatedAttributes[((AliasedValue)attribute.Value).AttributeLogicalName] = ((AliasedValue)attribute.Value).Value;
                            }
                        }
                        innerRelatedEntities.Add(j.ToString(), innerRelatedAttributes);
                        j++;
                    }

                    var entityData = new JSONEntity(name, relatedEntity.Id, relatedAttributes, innerRelatedEntities);
                    entitesList.Add(i.ToString(), entityData);
                    i++;
                }
                relatedEntities.Add(name, entitesList);
            }

            var formatedEntity = new JSONEntity(entity.LogicalName, entity.Id, tenderValues, relatedEntities);

            return Newtonsoft.Json.JsonConvert.SerializeObject(formatedEntity);
        }

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        public class JSONEntity
        {
            [JsonProperty("LogicalName")]
            string LogicalName;
            [JsonProperty("GUID")]
            Guid GUID;
            [JsonProperty("Attributes")]
            Dictionary<string, Object> Attributes;
            [JsonProperty("RelatedEntities")]
            Dictionary<string, Object> RelatedEntities;
            public JSONEntity(string logicalName, Guid gUID, Dictionary<string, object> attributes, Dictionary<string, object> relatedEntities)
            {
                LogicalName = logicalName;
                GUID = gUID;
                Attributes = attributes;
                RelatedEntities = relatedEntities;
            }
        }
    }
}
