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

namespace ExampleWorkflows
{
    public class CheckTenderBySystemCode : CodeActivity
    {
        [Input("Код внешней системы")]
        public InArgument<string> ExternalSystemCode { get; set; }

        [Output("Наличие")]
        public OutArgument<bool> IsExist { get; set; }

        [Output("Сообщение")]
        public OutArgument<string> Message { get; set; }
        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            string systemCode = ExternalSystemCode.Get(executionContext);
            if (systemCode == null)
            {
                Message.Set(executionContext, "Не введён Код внешней системы. ");
                return;
            }
            else
            {
                // Поиск и получение кол-ва записей с введённым кодом
                QueryExpression query = new QueryExpression("edu_tender");
                query.ColumnSet = new ColumnSet(new string[] { "edu_number" });
                query.Criteria.AddCondition("edu_external_system_code", ConditionOperator.Equal, systemCode);
                EntityCollection collection = service.RetrieveMultiple(query);
                if (collection.Entities.Count > 0)
                {
                    if (collection.Entities.Count > 1)
                    {
                        // Если более 1, то запись не уникальна по коду
                        IsExist.Set(executionContext, false);
                        Message.Set(executionContext, "По итогам проверки запись не уникальна. ");
                    }
                    else
                    {
                        // Если 1, то запись уникальна по коду
                        IsExist.Set(executionContext, true);
                        Message.Set(executionContext, "По итогам проверки, запись уникальна.");
                    }
                }
                else
                {
                    // Если 0, то записей с данным кодом нет
                    IsExist.Set(executionContext, false);
                    Message.Set(executionContext, "По итогам проверки, запись не была найдена по введённому коду внешней системы. ");
                }
            }
        }
    }
}
