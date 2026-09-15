using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExamplePlugins
{
    /// <summary>
    /// Пример плагина, запускающийся при создании новой записи
    /// Регистрируется на сущности lead
    /// Сообщение: Create
    /// Стадия: Post-operation
    /// Синхронный
    /// </summary>
    public class CreateTaskForLead : IPlugin
    {
        /// <summary>
        /// Данный плагин создаёт задачу для новосозданной записи Интереса
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
                if (context.InputParameters.Contains("Target"))
                {
                    // Получаем данные новосозданной сущности Интереса
                    Entity target = (Entity)context.InputParameters["Target"];

                    // Создаём запись сущности Задача
                    Entity taskRecord = new Entity("task");
                    // Указываем данные для создаваемой Задачи
                    // (название, описание, дата начала и конца, продолжительность и ссылка на запись Интереса)
                    taskRecord.Attributes.Add("subject", "Первичный анализ интереса " + target["subject"]);
                    taskRecord.Attributes.Add("description", "Провести первичный анализ данных по интересу.");
                    taskRecord.Attributes.Add("scheduledstart", DateTime.Now);
                    taskRecord.Attributes.Add("scheduledend", DateTime.Now.AddHours(1)); 
                    taskRecord.Attributes.Add("actualdurationminutes", 60);
                    taskRecord.Attributes.Add("regardingobjectid", target.ToEntityReference());
                    // Отправляем запрос на создания новой Задачи
                    service.Create(taskRecord);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Произошла ошибка при создании начальной задачи для интереса, ошибка: " + ex.Message);
            }
        }
    }
}
