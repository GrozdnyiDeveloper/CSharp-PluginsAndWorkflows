using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ExamplePlugins
{
    /// <summary>
    /// Пример плагина, запускающийся при изменении записи
    /// Регистрируется на сущности contact
    /// Сообщение: Update
    /// Поля: mobilephone, telephone1
    /// Стадия: Pre-operation
    /// Синхронный
    /// </summary>
    public class UpdatePhoneForContact : IPlugin
    {
        /// <summary>
        /// Данный плагин проверяет корректность введённых номеров телефона в контакте и меняет их под формат
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
                    // Получаем данные обновленной записи Контакт
                    Entity target = (Entity)context.InputParameters["Target"];

                    // Если при обновлении изменили поля "Рабочий телефон" или "Мобильный телефон"
                    if (target.Contains("mobilephone") || target.Contains("telephone1"))
                    {
                        // Задаём патерн regex для преобразования номеров в чисто числа
                        Regex regex = new Regex(@"\D");

                        // Указываем поля, валидацию которых будем проводить ("Рабочий телефон" и "Мобильный телефон")
                        string[] phoneFields = new string[] { "mobilephone", "telephone1" };

                        // Для каждого указанного поля
                        foreach (string field in phoneFields)
                        {
                            // вызываем метод валидации номера
                            CheckAndReformatPhone(target, field, regex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Произошла ошибка валидации номеров телефона контакта, ошибка: " + ex.Message);
            }
        }
        /// <summary>
        /// Метод валидации номеров телефонов (проверка номера и преобразование под формат)
        /// </summary>
        public void CheckAndReformatPhone(Entity target, string atributeName, Regex regex)
        {
            // Получаем значения переданного поля с телефоном
            var phone = target.GetAttributeValue<string>(atributeName);

            // Если поле содержит значения при обновлении 
            if (!string.IsNullOrEmpty(phone))
            {
                // Оставляем в номере только цифры
                phone = regex.Replace(phone, "");

                // Если номер не соответствует шаблону
                if (!Regex.IsMatch(phone, @"(7|8){1}[0-9]{10}$", RegexOptions.Compiled))
                    // Выдаём соответствующую ошибку
                    throw new InvalidPluginExecutionException("Введён некорректный номер телефона. ");

                // Изменяем номер, оставляя только цифры
                // Так как плагин вызван на событи Обновления записи и в стадии Pre-operation (т.е. до обновления),
                // то изменив значение поля в Target данные изменения побадут в систему через сам запрос, вызвавший данный плагин
                target[atributeName] = phone;
            }
        }
    }
}
