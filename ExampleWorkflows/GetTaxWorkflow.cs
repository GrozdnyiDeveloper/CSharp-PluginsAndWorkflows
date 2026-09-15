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
    public class GetTaxWorkflow : CodeActivity
    {
        [Input("key")]
        public InArgument<string> Key { get; set; }

        [Output("Tax")]
        public OutArgument<string> Tax { get; set; }
        protected override void Execute(CodeActivityContext executionContext)
        {
            //Create the tracing service
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            string key = Key.Get(executionContext);

            // Get data from configuration entity
            // Call organization web service
            QueryByAttribute query = new QueryByAttribute("edu_configuration");
            query.ColumnSet = new ColumnSet(new string[] { "edu_value" });
            query.AddAttributeValue("edu_name", key);
            EntityCollection collection = service.RetrieveMultiple(query);
            if (collection.Entities.Count != 1)
            {
                tracingService.Trace("Somethings wrong with configuration");
            }
            Entity config = collection.Entities.FirstOrDefault();
            tracingService.Trace(config.Attributes["edu_value"].ToString());
            Tax.Set(executionContext, config.Attributes["edu_value"].ToString());
        }
    }
}
