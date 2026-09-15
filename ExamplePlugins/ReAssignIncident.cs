using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExamplePlugins
{
    /// <summary>
    /// Пример плагина, запускающийся при назначении записи
    /// Регистрируется на сущности incident
    /// Сообщение: Assign
    /// Стадия: Pre-operation
    /// Синхронный
    /// Использует QueryExpression
    /// </summary>
    public class ReAssignIncident : IPlugin
    {
        /// <summary>
        /// Данный плагин при переназначении обращения создаёт примечание с пометкой на то, 
        /// кому оно было назначено, а также переназначает все дочерние обращения
        /// </summary>
        public void Execute(IServiceProvider serviceProvider)
        {
            // Получаем контекст плагина
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Получаем фактори сервиса
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            // Получаем сервис со стороны запустившего плагин пользователя
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);

            // Проверка: если плагин вызван из другого плагина
            // В данном случаи если плагин был вызван из самого себя при переназначении дочерних записей обращений
            if (context.Depth > 1)
            {
                // Прекращаем выполнение
                return;
            }

            try
            {
                if (context.InputParameters.Contains("Target"))
                {
                    // Получаем данные переназначенной сущности Обращения
                    // Так как плагин срабатывает на события Назначения (Assign), то Target в данном случаи вернёт только ссылку на запись (EntityReference)
                    EntityReference target = (EntityReference)context.InputParameters["Target"];
                    target.LogicalName = "incident";

                    // Получаем данные о новом ответственном за запись Обращения
                    var assignee = (EntityReference)context.InputParameters["Assignee"];

                    // Получаем данные о старом ответственном за запись Обращения
                    // Не смотря на то, что плагин вызывается на стадии Pre-operation (т.е. до самого события) из-за того, что событие Assign происходит мгновенно  
                    // образы Pre и Post image будут содержать запись после переназначения и для получения прошлого ответственного приходится отправлять запрос в CRM
                    var targetData = service.Retrieve(target.LogicalName, target.Id, new ColumnSet("ownerid"));
                    var assigneName = targetData.GetAttributeValue<EntityReference>("ownerid").Name;

                    // Создаём примечание об переназначении записи Обращения
                    Entity annotation = new Entity("annotation");
                    // Указываем название, текст примечания и назначаем её к текущей записи Обращения
                    annotation.Attributes.Add("subject", "Передача обращение");
                    annotation.Attributes.Add("notetext", $"Обращение было передано к {assigneName}. ");
                    annotation.Attributes.Add("objectid", target);
                    // Отправляем запрос на создания Примечания
                    service.Create(annotation);

                    // Создаём запрос на поиск записей дочерних обращений
                    QueryExpression query = new QueryExpression("incident");
                    query.NoLock = true;
                    query.ColumnSet = new ColumnSet("ownerid");
                    query.Criteria.AddCondition("parentcaseid", ConditionOperator.Equal, target.Id);
                    EntityCollection collection = service.RetrieveMultiple(query);

                    // Для каждого найденного дочернего обращения
                    foreach (Entity childIncident in collection.Entities)
                    {
                        // Переназначаем на нового ответственного
                        AssignRequest assignRequest = new AssignRequest();
                        assignRequest.Assignee = assignee;
                        assignRequest.Target = childIncident.ToEntityReference();
                        service.Execute(assignRequest);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Произошла ошибка при переназначении дочерних обращений, ошибка: " + ex.Message);
            }
        }
    }
}
