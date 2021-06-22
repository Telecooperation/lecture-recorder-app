using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TK_Recorder.Model
{
    public class Lecture
    {
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "varchar(255)")]
        public string Title { get; set; }

        [Column(TypeName = "text")]
        public string Description { get; set; }

        [Required]
        public int SemesterId { get; set; }

        public Semester Semester { get; set; }

        public string SourcePath { get; set; }

        public string PublishPath { get; set; }

        public DateTime? LastSynchronized { get; set; }

        public bool Publish { get; set; }

        public bool Active { get; set; }
    }
}
