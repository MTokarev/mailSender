using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace mailSender
{
    class Ex3
    {
        [JsonPropertyName("DOB")]
        public string DobStr { get; set; }

        [JsonIgnore]
        public DateTime Dob
        {
            get
            {
                DateTime.TryParse(DobStr, out var value);
                return value;
            }
            set
            {
                DobStr = value.ToString();
            }
        }
    }
}
