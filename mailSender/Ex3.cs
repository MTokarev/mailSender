using System;
using System.Globalization;
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
                // We expect to have string in the specific format 'dd/MM/yyyy'
                if (DateTime.TryParseExact(DobStr,
                    "dd/MM/yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var value))
                {
                    return value;
                }

                return DateTime.MinValue;
            }
            set
            {
                DobStr = value.ToString();
            }
        }
    }
}
