using DataLayer.Generators;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
{
    [Table("session")]
    public class Session : Entity<Session>
    {
        [Key]
        [Required]
        [MaxLength(128)]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        public DateTime Time { get; set; }

        public int Lifetime { get; set; } = 31536000;

        /// <summary>
        /// Maps to @ORM\OneToMany. 
        /// Use ICollection for EF Core navigation properties.
        /// </summary>
        public virtual ICollection<Visit> Visits { get; set; }

        // there is no Users on purpose
        //public virtual ICollection<Users> Users { get; set; }


        public Session()
        {
            Visits = new HashSet<Visit>();
            Id = Guid.NewGuid().ToString();
            Time = DateTime.UtcNow;
        }
    }
}