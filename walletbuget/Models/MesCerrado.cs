using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace walletbuget.Models
{
    public class MesCerrado
    {
        [Key]
        public int Id { get; set; }

        public int Año { get; set; }
        public int Mes { get; set; }

        [ForeignKey("ApplicationUser")]
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
