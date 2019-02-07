using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Actors.Interfaces.Models.Validation
{
    /// <summary>
    /// Validates the Cosmos DB RU value.
    /// </summary>
    public class CosmosRUAttribute : ValidationAttribute
    {
        /// <inherit/>
        public override bool IsValid(object value)
        {
            if (value == null)
                return true;

            int ru = (int)value;
            return ru >= 400 && ru % 100 == 0;
        }
    }
}
