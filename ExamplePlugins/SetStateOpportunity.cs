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
    /// Пример плагина, запускающийся при изменении состояния записи Возможной сделки
    /// Регистрируется на сущности opportunity
    /// Сообщение: SetState
    /// Стадия: Post-operation
    /// Синхронный
    /// Использует образы PreImage и PostImage
    /// Своим действием вызывает плагин UpdateOpportunity
    /// </summary>
    public class SetStateOpportunity : IPlugin
    {
        /// <summary>
        /// Данный плагин в зависимости от смены статуса завки Возможной сделки устанавливает значение поля "Текущая ситуация" 
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
                // Проверка на наличие и получение образов preImage и postImage
                if (!context.PreEntityImages.Contains("preImage")) throw new Exception("Отсутствует preImage");
                Entity preImage = context.PreEntityImages["preImage"];
                if (!context.PostEntityImages.Contains("postImage")) throw new Exception("Отсутствует postImage");
                Entity postImage = context.PostEntityImages["postImage"];

                // Получение прошлого и нового статусов из образов
                var preState = preImage.GetAttributeValue<OptionSetValue>("statecode");
                var postState = postImage.GetAttributeValue<OptionSetValue>("statecode");

                // Если новый статус "Открыта" (Повторное открытие)
                if (postState.Value == 0)
                {
                    // Обновляем текущую запись Сделки, изменяем поле "Текущая ситуация" на "Повторное открытие"
                    Entity opportunityUpdate = new Entity("opportunity");
                    opportunityUpdate.Id = postImage.Id;
                    opportunityUpdate["currentsituation"] = "Повторное открытие. ";
                    service.Update(opportunityUpdate);
                }
                // Иначе если прошлый статус "Открыта" (Закрытие сделки)
                else if (preState.Value == 0)
                {
                    // Если новый статус "Сделка заключена" (Закрытие как реализованной)
                    if (postState.Value == 1)
                    {
                        // Обновляем текущую запись Сделки, изменяем поле "Текущая ситуация" на "Удачное закрытие"
                        Entity opportunityUpdate = new Entity("opportunity");
                        opportunityUpdate.Id = postImage.Id;
                        opportunityUpdate["currentsituation"] = "Удачное закрытие. ";
                        service.Update(opportunityUpdate);
                    }
                    // Иначе если новый статус "Потерена" (Закрытие как нереализованной)
                    else if (postState.Value == 2)
                    {
                        // Обновляем текущую запись Сделки, изменяем поле "Текущая ситуация" на "Неудачное закрытие"
                        Entity opportunityUpdate = new Entity("opportunity");
                        opportunityUpdate.Id = postImage.Id;
                        opportunityUpdate["currentsituation"] = "Неудачное закрытие. ";
                        service.Update(opportunityUpdate);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Произошла ошибка при изменении статуса записи Возможной сделки, ошибка: " + ex.Message);
            }
        }
    }
}
