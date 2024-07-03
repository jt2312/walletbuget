namespace walletbuget.Models.Dto
{
    public class CuentaMostrarDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int TipoCuenta { get; set; }
        public decimal Balance { get; set; }
        public string Descripcion { get; set; }
    }
}
