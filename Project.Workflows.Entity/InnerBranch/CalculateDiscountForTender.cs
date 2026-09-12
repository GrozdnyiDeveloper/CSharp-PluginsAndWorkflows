using System.Activities;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Project.Workflows.EntityItem.InnerBranch
{
    public class CalculateDiscountForTender : CodeActivity
    {
        [Input("Направление деятельности Project")]
        [ArgumentDescription("Направление деятельности по которому берутся продукты")]
        [ReferenceTarget("iek_inner_branch")]
        public InArgument<EntityReference> Branch { get; set; }

        [Input("Тендер")]
        [RequiredArgument]
        [ArgumentDescription("Тендер из которого берём продукты")]
        [ReferenceTarget("opportunity")]
        public InArgument<EntityReference> Opportunity { get; set; }

        [Output("Скидки по направлению для Тендера")]
        public OutArgument<int> DiscountPercent { get; set; }

        [Output("Скидки найдены")]
        public OutArgument<bool> IsDiscountFound { get; set; }


        protected override void Execute(CodeActivityContext executionContext)
        {
            var context = executionContext.GetExtension<IWorkflowContext>();
            var serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            // Получаем данные направления деятельности и внешнего проекта
            var opportunityRef = Opportunity.Get<EntityReference>(executionContext);
            var branchRef = Branch.Get<EntityReference>(executionContext);
            var result = InternalRun(opportunityRef, branchRef, service);

            // Возвращаем полученное значение скидки и факт того было ли оно высчитано
            DiscountPercent.Set(executionContext, result.Item1);
            IsDiscountFound.Set(executionContext, result.Item2);
        }

        (int, bool) InternalRun(EntityReference opportunityRef, EntityReference branchRef, IOrganizationService service)
        {
            // Получаем строку с фильтрацией по направлению деятельности
            var filterFetchXml = GetParentProductFilterByBranch(branchRef);
            // Формируем запрос на получение продуктов внешнего проекта и агрегирования среднего значения скидки
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                <fetch aggregate=""true"">
                                  <entity name=""opportunityproduct"">
                                    <attribute name=""iek_required_discount"" alias=""discount"" aggregate=""avg"" />
                                    <filter>
                                      <condition attribute=""opportunityid"" operator=""eq"" value=""{opportunityRef.Id}"" />
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
                    return (Convert.ToInt32(discount), true);
            }
            // Иначе возвращаем 0
            return (0, false);
        }

        private string GetParentProductFilterByBranch(EntityReference branchRef)
        {
            // Если в кастомный шаг не передано направление деятельности
            if (branchRef == null)
                // То возрващаем пустую строку
                return string.Empty;
            else
                // Иначе добавляем в fetch фильтрацию по направлению деятельности через родительский элемент
                return $@"<link-entity name=""product"" from=""productid"" to=""productid"" alias=""product"">
                            <link-entity name=""product"" from=""productid"" to=""iek_parent_product"" alias=""tg"">
                              <filter>
                                <condition attribute = ""iek_inner_branchid"" operator= ""eq"" value = ""{branchRef.Id}"" />
                              </filter>
                            </link-entity>
                          </link-entity>";
        }
    }
}
