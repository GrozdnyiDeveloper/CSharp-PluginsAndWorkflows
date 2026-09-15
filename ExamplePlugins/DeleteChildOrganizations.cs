using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExamplePlugins
{
    /// <summary>
    /// Пример плагина, запускающийся при удалении записи
    /// Регистрируется на сущности account
    /// Сообщение: Delete
    /// Стадия: Pre-validation
    /// Синхронный
    /// Использует QueryExpression
    /// Использует образ PreImage
    /// </summary>
    public class DeleteChildOrganizations : IPlugin
    {
        /// <summary>
        /// Данный плагин для всех дочерних организаций добавляет текст в описании об удалении родительской 
        /// </summary>
        public void Execute(IServiceProvider serviceProvider)
        {
            // Получаем контекст плагина
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Получаем фактори сервиса
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            // Получаем сервис со стороны запустившего плагин пользователя
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);

            try
            {
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    // Получаем образ записи Организации до удаления
                    Entity preImageEntity = (Entity)context.PreEntityImages["PreImage"];

                    // Создаём запрос на поиск записей дочерних организаций
                    QueryExpression query = new QueryExpression("account");
                    query.NoLock = true;
                    query.ColumnSet = new ColumnSet("accountid", "description");
                    query.Criteria.AddCondition("parentaccountid", ConditionOperator.Equal, preImageEntity.Id);
                    EntityCollection collection = service.RetrieveMultiple(query);

                    // Для каждой найденной дочерней организации
                    foreach (Entity childOrg in collection.Entities)
                    {
                        // Добавляем в описание пометку о том, что родительская запись была удалена
                        childOrg["description"] = $"Данная организация является дочерней к {preImageEntity.GetAttributeValue<string>("name")}, которая была удалена. \n" + childOrg.GetAttributeValue<string>("description");
                        // И обновляем её
                        service.Update(childOrg);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Произошла ошибка при заполнении описаний дочерних записей, ошибка: " + ex.Message);
            }
        }
    }
}
