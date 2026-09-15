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
    public class GetTenderWorkflow : CodeActivity
    {
        [Input("GUID")]
        public InArgument<string> GUID { get; set; }

        [Output("Тендер")]
        [ReferenceTarget("edu_tender")]
        public OutArgument<EntityReference> TenderRef { get; set; }
        protected override void Execute(CodeActivityContext executionContext)
        {
            //Create the tracing service
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            string guidString = GUID.Get(executionContext);

            Entity tender = service.Retrieve("edu_tender", Guid.Parse(guidString), new ColumnSet(allColumns: true));

            TenderRef.Set(executionContext, tender.ToEntityReference());
        }
    }
}
