namespace Oloraculo.Web.Models
{
    public class ApiMapping
    {
        public int Id { get; set; }
        public string LocalFixtureId { get; set; }
        public string ExternalFixtureId { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

    }
}
