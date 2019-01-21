using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Actors.Interfaces.Models.Validation
{
    public class CosmosRUAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value == null)
                return true;

            int ru = (int)value;
            return ru >= 400 && ru % 100 == 0;
        }
    }
}
