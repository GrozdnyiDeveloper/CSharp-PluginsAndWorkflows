using Dynamics.Plugins.Common;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynamics.Plugins.external_projects
{
    public class CheckSourcetypeForProject : PluginBase
    {
        protected override void Execute(PluginContext context)
        {
            var service = context.GetOrganizationService();
            var target = ((Entity)context.InputParameters["Target"]);

            // Получаем данные внешнего проекта (из Target при создании и из PreImage при изменении) 
            var externalProject = context.ExecutionContext.PreEntityImages.Contains("Image") ? (Entity)context.ExecutionContext.PreEntityImages["Image"] : ((Entity)context.InputParameters["Target"]); ;
            var projectId = externalProject.GetAttributeValue<EntityReference>("iek_project")?.Id;
            var sourcetypeCode = externalProject.GetAttributeValue<OptionSetValue>("iek_sourcetype")?.Value;

            // Если поле "Проект" содержит данные и "Источник поступления" = "DBP"
            if (projectId != null && sourcetypeCode == 279750003)
            {
                // То отменяем действие и выводим сообщение об ошибке
                throw new InvalidPluginExecutionException("Запрет на изменение поля. \n\nИзменение поля \"Проект\" недоступно. Сформирована Заявка на регистрацию/защиту DBP. \n\n(код ошибки KB000000000W) \n\n");
            }
        }
    }
}
