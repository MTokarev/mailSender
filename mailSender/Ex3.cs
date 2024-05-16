using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace mailSender
{
    class Ex3
    {
        [JsonPropertyName("DOB")]
        public string DobStr { get; set; }
        
        private DateTime _dob;
        private bool _isDobFormatted = false;

        [JsonIgnore]
        public DateTime Dob
        {
            get
            {
                // If we already tried to parse the date, then return cached result
                if (_isDobFormatted)
                {
                    return _dob;
                }

                // We expect to have string in the specific format 'M/d/yyyy'
                // e.g. '2/3/1990', '11/12/1985'
                if (DateTime.TryParseExact(DobStr,
                    "M/d/yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var value))
                {
                    _dob = value;
                }

                _isDobFormatted = true;
                return _dob;
            }
            set
            {
                DobStr = value.ToString();
            }
        }
    }
}
