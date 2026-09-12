using System.Activities;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Project.Workflows.EntityItem.InnerBranch
{
    public class CalculateDiscountForExternalProject : CodeActivity
    {
        [Input("Направление деятельности Project")]
        [ArgumentDescription("Направление деятельности по которому берутся продукты")]
        [ReferenceTarget("iek_inner_branch")]
        public InArgument<EntityReference> Branch { get; set; }

        [Input("Внешний проект")]
        [RequiredArgument]
        [ArgumentDescription("Внешний проект из которого берём продукты")]
        [ReferenceTarget("iek_etm_project")]
        public InArgument<EntityReference> ExtProject { get; set; }

        [Output("Скидки по направлению для Заявок")]
        public OutArgument<int> DiscountPercent { get; set; }


        protected override void Execute(CodeActivityContext executionContext)
        {
            var context = executionContext.GetExtension<IWorkflowContext>();
            var serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            // Получаем данные направления деятельности и внешнего проекта
            var projectRef = ExtProject.Get<EntityReference>(executionContext);
            var branchRef = Branch.Get<EntityReference>(executionContext);
            var result = InternalRun(projectRef, branchRef, service);

            // Возвращаем полученное среднее значение скидки
            DiscountPercent.Set(executionContext, result);
        }

        private int InternalRun(EntityReference projectRef, EntityReference branchRef, IOrganizationService service)
        {
            // Получаем строку с фильтрацией по направлению деятельности
            var filterFetchXml = GetProductFilterByBranch(branchRef);
            // Формируем запрос на получение продуктов внешнего проекта и агрегирования среднего значения скидки
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                <fetch aggregate=""true"">
                                  <entity name=""iek_extproject_product"">
                                    <attribute name=""iek_discount"" alias=""discount"" aggregate=""avg"" />
                                    <filter>
                                      <condition attribute=""iek_ext_projectid"" operator=""eq"" value=""{projectRef.Id}"" />
                                    </filter>
                                    {filterFetchXml}
                                  </entity>
                                </fetch>";
            var result = service.RetrieveMultiple(new FetchExpression(fetchXml));

            // Если получили результат
            if (result.Entities.Count != 0)
            {
                // Получаем среднее значение скидки
                var discount = result.Entities[0].GetAttributeValue<AliasedValue>("discount")?.Value;
                if (discount != null)
                    // Если получили, то конвертируем в целое число и возвращаем
                    return Convert.ToInt32(discount);
            } 
            // Иначе возвращаем 0
            return 0;
        }

        private string GetProductFilterByBranch(EntityReference branchRef)
        {
            // Если в кастомный шаг не передано направление деятельности
            if (branchRef == null)
                // То возрващаем пустую строку
                return string.Empty;
            else
                // Иначе добавляем в fetch фильтрацию по направлению деятельности
                return $@"<link-entity name=""product"" from=""productid"" to=""iek_productid"" alias=""product"">
                            <filter>
                              <condition attribute = ""iek_inner_branchid"" operator= ""eq"" value = ""{branchRef.Id}"" />
                            </filter>
                          </link-entity>";
        }
    }
}
