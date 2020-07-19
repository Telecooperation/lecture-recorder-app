using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SimpleRecorder.Model
{
    public class Semester
    {
        public int Id { get; set; }

        [Column(TypeName = "varchar(255)")]
        public string Name { get; set; }

        public DateTime DateStart { get; set; }

        public DateTime DateEnd { get; set; }

        public bool Published { get; set; }

        public bool Active { get; set; }
    }
}
