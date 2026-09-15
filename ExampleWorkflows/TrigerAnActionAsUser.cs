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
using Microsoft.Crm.Sdk.Messages;
using System.Collections;
using Microsoft.Xrm.Sdk.Client;

namespace ExampleWorkflows
{
    public class TrigerAnActionAsUser : CodeActivity
    {
        [Input("Название действия")]
        public InArgument<string> ActionName { get; set; }

        [Input("Пользователь")]
        [ReferenceTarget("systemuser")]
        public InArgument<EntityReference> SelectedUser { get; set; }

        [Input("Запись")]
        [ReferenceTarget("edu_tender")]
        public InArgument<EntityReference> SelectedTender { get; set; }

        [Output("Итоговый результат")]
        public OutArgument<string> Message { get; set; }
        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            EntityReference selectedUser = SelectedUser.Get(executionContext);
            if (selectedUser == null) {
                Message.Set(executionContext, "Пользователь для запуска не найден. ");
                return;
            }
            else 
            {
                IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
                IOrganizationServiceFactory ServiceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
                OrganizationServiceProxy service = (OrganizationServiceProxy)ServiceFactory.CreateOrganizationService(selectedUser.Id);
                service.CallerId = selectedUser.Id;

                string actionName = ActionName.Get(executionContext);
                if (selectedUser == null)
                {
                    Message.Set(executionContext, "Не введено название действия. ");
                    return;
                }
                else
                {
                    // Запрос для получения id процесса
                    QueryExpression queryExpression = new QueryExpression("workflow");
                    queryExpression.ColumnSet = new ColumnSet("workflowid");
                    queryExpression.Criteria.AddCondition("name", ConditionOperator.Equal, actionName);
                    queryExpression.Criteria.AddCondition("businessprocesstype", ConditionOperator.NotEqual, 0);
                    EntityCollection collection = service.RetrieveMultiple(queryExpression);
                    // Проверка на наличие процесса
                    if (collection.Entities.Count > 0)
                    {
                        Guid workflowId = collection.Entities[0].GetAttributeValue<Guid>("workflowid");
                        EntityReference selectedTender = SelectedTender.Get(executionContext);

                        // Создание запроса на запуск процесса
                        ExecuteWorkflowRequest request = new ExecuteWorkflowRequest();
                        request.WorkflowId = workflowId;
                        request.EntityId = selectedTender.Id;
                        var result = (ExecuteWorkflowResponse)service.Execute(request);
                        Message.Set(executionContext, "Действие успешно завершено. ");
                    }
                    else
                    {
                        Message.Set(executionContext, "Действие для запуска не найдено. ");
                    }
                }
            }
        }
    }
}
