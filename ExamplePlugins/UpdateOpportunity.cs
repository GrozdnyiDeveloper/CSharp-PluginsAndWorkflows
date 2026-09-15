using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Query;

namespace ExamplePlugins
{
    /// <summary>
    /// Пример плагина, запускающийся при обновлении записи Возможной сделки
    /// Регистрируется на сущности opportunity
    /// Сообщение: Update
    /// Поля: currentsituation
    /// Стадия: Post-operation
    /// Синхронный
    /// Вызывается из плагина SetStateOpportunity
    /// </summary>
    public class UpdateOpportunity : IPlugin
    {
        /// <summary>
        /// Данный плагин логирует в описание записи Возможной сделки изменения поля "Текущая ситуация"
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
            // В данном случаи если плагин был вызван из плагина изменения состояния Возможной сделки (SetStateOpportunity)
            if (context.Depth > 1)
            {
                // Прекращаем выполнение
                return;
            }

            try
            {
                if (context.InputParameters.Contains("Target"))
                {
                    // Получаем данные обновленной записи Возможной сделки
                    Entity target = (Entity)context.InputParameters["Target"];

                    // Если при обновлении изменили поле "Текущая ситуация"
                    if (target.Contains("currentsituation"))
                    {
                        // Получаем текущее описание сущности
                        // Target не будет  содержать поле "Описание" (description), если оно не было изменено при событии
                        var currentSituation = target.GetAttributeValue<string>("currentsituation");
                        var oldDescription = service.Retrieve("opportunity", target.Id, new ColumnSet("description")).GetAttributeValue<string>("description");

                        // Изменяем описание записи Возможной сделки, добавляя в неё лог изменения поля "Текущая ситуация" 
                        Entity opportunity = new Entity("opportunity");
                        opportunity.Id = target.Id;
                        opportunity["description"] = oldDescription + "\nПользовательское изменение текущей ситуации по заявке на " + currentSituation + ": " + DateTime.Now;
                        service.Update(opportunity);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Произошла ошибка при логировании изменения текущей ситуации в описание заявки Возможной сделки, ошибка: " + ex.Message);
            }
        }
    }
}
