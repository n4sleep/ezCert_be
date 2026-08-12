using EzCert.Processor.Features.Exams;

namespace EzCert.Processor.Features.Generation;

// Vertical-slice mini bank: deterministic 5-question exams so the full loop
// (chat -> job -> exam -> take -> results) works before Bedrock generation lands.
public static class MiniBank
{
    private static readonly (string Q, string[] Correct, (string L, string T)[] Choices, string Expl, string Src)[] Items =
    {
        ("Which statement best describes cloud computing?",
            new[] { "a" },
            new[] { ("a", "Delivery of computing services over the internet"), ("b", "A single physical server in an office"), ("c", "Desktop software installed locally"), ("d", "A private network with no internet") },
            "Cloud computing delivers computing services over the internet with pay-as-you-go pricing.",
            "https://learn.microsoft.com/en-us/training/modules/describe-cloud-compute/3-what-cloud-compute"),
        ("Which are ALWAYS the customer's responsibility under the shared responsibility model? (Select all that apply.)",
            new[] { "a", "b", "c" },
            new[] { ("a", "The information and data stored in the cloud"), ("b", "The accounts and identities of users"), ("c", "The devices allowed to connect"), ("d", "The physical datacenter and hosts") },
            "Customers always own data, identities, and devices; the provider owns physical infrastructure.",
            "https://learn.microsoft.com/en-us/training/modules/describe-cloud-compute/4-describe-shared-responsibility-model"),
        ("Which cloud service type places the MOST responsibility on the consumer?",
            new[] { "a" },
            new[] { ("a", "Infrastructure as a service (IaaS)"), ("b", "Platform as a service (PaaS)"), ("c", "Software as a service (SaaS)"), ("d", "All types share equally") },
            "IaaS leaves the most responsibility with the consumer; SaaS leaves the most with the provider.",
            "https://learn.microsoft.com/en-us/training/modules/describe-cloud-compute/4-describe-shared-responsibility-model"),
        ("True or False: In a multicloud scenario, an organization uses two or more public cloud providers.",
            new[] { "a" },
            new[] { ("a", "True"), ("b", "False") },
            "Multicloud means using multiple public cloud providers.",
            "https://learn.microsoft.com/en-us/training/modules/describe-cloud-compute/5-define-cloud-models"),
        ("Which service type is a managed relational database?",
            new[] { "a" },
            new[] { ("a", "Platform as a service (PaaS)"), ("b", "Infrastructure as a service (IaaS)"), ("c", "Software as a service (SaaS)"), ("d", "On-premises only") },
            "Managed databases are platform services: the provider manages the platform, the consumer manages data.",
            "https://learn.microsoft.com/en-us/training/modules/describe-cloud-service-types/4-describe-platform-as-a-service"),
    };

    public static Exam BuildExam(string deviceId, string prompt)
    {
        var exam = new Exam
        {
            OwnerDeviceId = deviceId,
            Title = "AZ-900 Cloud Concepts Practice",
            Description = $"Generated from: {prompt}",
            Mode = "practice",
            Difficulty = "medium",
            DurationMinutes = 10,
            Status = "ready",
            GenerationPrompt = prompt,
            ExpiresAt = DateTime.UtcNow.AddDays(3),
            ShareToken = null,
        };

        var ord = 0;
        foreach (var (q, correct, choices, expl, src) in Items)
        {
            var question = new Question
            {
                Ordinal = ord++,
                Type = correct.Length > 1 ? "multi" : correct.Length == 1 && choices.Length == 2 && (choices[0].T == "True" || choices[0].T == "False") ? "truefalse" : "single",
                Text = q,
                Explanation = expl,
            };
            var cOrd = 0;
            foreach (var (label, text) in choices)
            {
                question.Choices.Add(new Choice { Label = label, Text = text, IsCorrect = correct.Contains(label), Ordinal = cOrd++ });
            }
            question.Citations.Add(new QuestionCitation { SourceUrl = src, QuotedText = expl });
            exam.Questions.Add(question);
        }
        return exam;
    }
}
