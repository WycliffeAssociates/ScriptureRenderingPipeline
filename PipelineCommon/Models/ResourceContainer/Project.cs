using YamlDotNet.Serialization;

namespace PipelineCommon.Models.ResourceContainer
{
    public class Project
    {
        public string title { get; set; }
        public string versification { get; set; }
        public string identifier { get; set; }

        [YamlMember(Alias ="sort")]
        public string sortAsString { get; set; }
        [YamlIgnore]
        public int sort
        {
            get
            {
                if (int.TryParse(sortAsString, out int result))
                {
                    return result;
                }
                throw new System.Exception("Sort isn't a valid number");
            }
            set
            {
                sortAsString = value.ToString();
            }
        }
        public string path { get; set; }
        public string[] categories { get; set; }
    }
}
