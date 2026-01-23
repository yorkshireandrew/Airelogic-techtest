using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace HealthTest;

public class QuestionnaireFormParser
{
    public QuestionnaireFormModel Parse(IFormCollection form)
    {
        var model = new QuestionnaireFormModel();

        // Age band
        var ageBandStr = form["AgeBand"].ToString();
        if (int.TryParse(ageBandStr, out var ab)) model.AgeGroup = ab;

        // Total questions - prefer explicit field, otherwise infer from keys
        var totalStr = form["TotalQuestions"].ToString();
        int total = 0;
        if (!int.TryParse(totalStr, out total))
        {
            var max = -1;
            var regex = new Regex("^[YN](\\d+)$");
            foreach (var key in form.Keys)
            {
                var m = regex.Match(key);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var idx))
                {
                    if (idx > max) max = idx;
                }
            }
            total = max + 1;
            if (total < 0) total = 0;
        }

        model.Answers = new List<bool>(total);
        for (int i = 0; i < total; i++)
        {
            var yKey = $"Y{i}";
            var ySelected = form.TryGetValue(yKey, out StringValues v) && !StringValues.IsNullOrEmpty(v);
            model.Answers.Add(ySelected);
        }

        return model;
    }
}
