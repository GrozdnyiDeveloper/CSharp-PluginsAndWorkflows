using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Microsoft.Crm.Sdk.Messages;

namespace ExampleWorkflows
{
    public class StartWorkflow : CodeActivity
    {
        [Input("Заявка")]
        [ReferenceTarget("edu_request")]
        public InArgument<EntityReference> SelectedRequest { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            var administratorId = Guid.Parse("55a4bc12-f37e-ef11-a317-001dd8bb287f");

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory ServiceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            OrganizationServiceProxy service = (OrganizationServiceProxy)ServiceFactory.CreateOrganizationService(administratorId);
            service.CallerId = administratorId;

            Guid workflowId = Guid.Parse("f192f300-d6ee-48eb-9a2f-0fc98f840006");
            EntityReference selectedRequest = SelectedRequest.Get(executionContext);

            // Создание запроса на запуск процесса
            ExecuteWorkflowRequest request = new ExecuteWorkflowRequest();
            request.WorkflowId = workflowId;
            request.EntityId = selectedRequest.Id;

            service.Execute(request);
        }
    }
}
