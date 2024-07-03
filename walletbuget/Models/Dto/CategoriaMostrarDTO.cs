using System.ComponentModel.DataAnnotations;

namespace walletbuget.Models.Dto
{
    public class CategoriaMostrarDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int TipoOperacion { get; set; }
    }
}
