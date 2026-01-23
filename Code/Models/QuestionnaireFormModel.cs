using System.Collections.Generic;

namespace HealthTest;

public class QuestionnaireFormModel
{
    public int AgeGroup { get; set; }
    public List<bool> Answers { get; set; } = new List<bool>();
}
