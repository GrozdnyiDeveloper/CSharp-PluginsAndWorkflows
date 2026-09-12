using System;
using Microsoft.Xrm.Sdk;
using Dynamics.Plugins.Common;


namespace Dynamics.Plugins.opportunityproduct
{
    public class CalculateClientNDS : PluginBase
    {
        protected override void Execute(PluginContext context)
        {
            if (context.ExecutionContext.MessageName.ToUpper() != "UPDATE")
                return;

            // Получаем данные из набора изменений и снимка до изменений
            var target = (Entity)context.InputParameters["Target"];
            var preImage = context.ExecutionContext.PreEntityImages["Image"];

            try
            {
                // Если было изменено поле "Скидка клиента, %"
                if (target.Attributes.Contains("iek_customer_discount"))
                {
                    // Получаем данные продукта тендера (из target или preimage)
                    var productData = GetProductData(target, preImage);
                    var baseprice = GetMoney(productData, "iek_baseprice");
                    var quantity = productData.GetAttributeValue<int>("iek_round_quantity");

                    // Если поле содержит значение (не было очищено при изменении)
                    var discount = target.GetAttributeValue<Decimal?>("iek_customer_discount");
                    if (discount != null)
                    {
                        // Заполняем поле "Сумма с НДС клиента" по формуле: "Базовая цена продукта" * (100 — "Скидка клиента, %") / 100 * "Округленное кол-во"
                        target["iek_customer_amountvat"] = baseprice * (100 - discount) / 100 * quantity;
                    }
                    else
                    {
                        // Заполняем поле "Сумма с НДС клиента" по формуле: "Базовая цена продукта" * 100 * "Округленное кол-во"
                        target["iek_customer_amountvat"] = baseprice * 100 * quantity;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(OperationStatus.Failed, ex.Message);
            }
        }


        // Функция по получению данных продукта из target или image
        private Entity GetProductData(Entity target, Entity preImage)
        {
            var productData = new Entity(target.LogicalName, target.Id);

            // Если "Базовая цена продукта" содержится в наборе изменений
            if (target.Contains("iek_baseprice"))
                // То получаем поле "Базовая цена продукта" из набора изменений
                productData["iek_baseprice"] = GetMoney(target, "iek_baseprice");
            else
                // Иначе получаем поле "Базовая цена продукта" из снимка до изменений
                productData["iek_baseprice"] = GetMoney(preImage, "iek_baseprice");

            // Если "Округленное кол-во" содержится в наборе изменений
            if (target.Contains("iek_round_quantity"))
                // То получаем поле "Округленное кол-во" из набора изменений
                productData["iek_round_quantity"] = target.GetAttributeValue<int>("iek_round_quantity");
            else
                // Иначе получаем поле "Округленное кол-во" из снимка до изменений
                productData["iek_round_quantity"] = preImage.GetAttributeValue<int>("iek_round_quantity");

            return productData;
        }


        private decimal GetMoney(Entity entity, string attributeName)
        {
            var value = entity[attributeName];

            if (value is Money money)
                return money.Value;

            return (decimal)value;
        }
    }
}
