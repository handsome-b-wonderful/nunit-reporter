

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

namespace NUnitReporter
{
    public class Program
    {
        private static readonly Regex Regex = new Regex("[^a-zA-Z0-9 -]");

        private enum FixtureStatus { pass, fail, skip };

        static void Main(string[] args)
        {
            try
            {
                var html = new StringBuilder();
                var input = string.Empty;
                var output = string.Empty;

                if (args.Length == 0)
                {
                    Console.WriteLine("Usage: nreporter.exe [input-file] [output-file]");
                }
                else
                {
                    input = args[0];
                    if (!File.Exists(input))
                    {
                        Console.WriteLine(string.Format("Input file \"{0}\" not found.", input));
                    }
                    else
                    {
                        if (args.Length > 1)
                        {
                            output = args[1];
                        }
                        else
                        {
                            output = Path.ChangeExtension(input, "html");
                        }

                        html.Append(GetHTML5Header("Results"));
                        html.Append(ProcessFile(input));
                        html.Append(GetHTML5Footer());
                        File.WriteAllText(output, html.ToString());
                    }
                }
            }

            catch (Exception ex)
            {
                Console.Write(string.Format("Error generating HTML report: {0}", ex));
            }
        }

        private static string ProcessFile(string file)
        {
            StringBuilder html = new StringBuilder();
            XElement doc = XElement.Load(file);

            // Load summary values
            string testName = doc.Attribute("name").Value;
            int testTests = int.Parse(!(doc.Attribute("total") == null || string.IsNullOrEmpty(doc.Attribute("total").Value)) ? doc.Attribute("total").Value : "0");
            int testFailures = int.Parse(!(doc.Attribute("failures") == null || string.IsNullOrEmpty(doc.Attribute("failures").Value)) ? doc.Attribute("failures").Value : "0");
            int testSkipped = int.Parse(!(doc.Attribute("not-run") == null || string.IsNullOrEmpty(doc.Attribute("not-run").Value)) ? doc.Attribute("not-run").Value : "0");
            DateTime testDate = DateTime.Parse(string.Format("{0} {1}", doc.Attribute("date").Value, doc.Attribute("time").Value));

            // Calculate the success rate
            decimal percentage = 0;
            if (testTests > 0)
            {
                int failures = testFailures;
                percentage = decimal.Round(decimal.Divide(failures, testTests) * 100, 1);
            }

            // Container
            html.AppendLine("<div class=\"container-fluid page\">");

            // Summary panel
            html.AppendLine("<div class=\"row\">");
            html.AppendLine("<div class=\"col-md-12\">");
            html.AppendLine("<div class=\"panel panel-default\">");
            html.AppendLine(string.Format("<div class=\"panel-heading\">Summary - <small>{0}</small></div>", testName));
            html.AppendLine("<div class=\"panel-body\">");

            html.AppendLine(string.Format("<div class=\"col-md-2 col-sm-4 col-xs-3 text-center\"><div class=\"stat\">Tests</div><div class=\"val ignore-val\">{0}</div></div>", testTests));
            html.AppendLine(string.Format("<div class=\"col-md-2 col-sm-4 col-xs-3 text-center\"><div class=\"stat\">Failures</div><div class=\"val {1}\">{0}</div></div>", testFailures, testFailures > 0 ? "text-danger" : string.Empty));
            html.AppendLine(string.Format("<div class=\"col-md-2 col-sm-4 col-xs-3 text-center\"><div class=\"stat\">Skipped</div><div class=\"val {1}\">{0}</div></div>", testSkipped, testSkipped > 0 ? "text-danger" : string.Empty));
            html.AppendLine(string.Format("<div class=\"col-md-2 col-sm-4 col-xs-3 text-center\"><div class=\"stat\">Date</div><div class=\"val\">{0}</div></div>", testDate.ToString("d MMM")));
            html.AppendLine(string.Format("<div class=\"col-md-2 col-sm-4 col-xs-3 text-center\"><div class=\"stat\">Time</div><div class=\"val\">{0}</div></div>", testDate.ToShortTimeString()));
            html.AppendLine(string.Format("<div class=\"col-md-2 col-sm-4 col-xs-3 text-center\"><div class=\"stat\">Success Rate</div><div class=\"val\">{0}%</div></div>", 100 - percentage));

            // End summary panel
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            // Process test fixtures
            html.Append(ProcessFixtures(doc.Descendants("test-suite")));

            // End container
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            return html.ToString();
        }

        private static string ProcessFixtures(IEnumerable<XElement> fixtures)
        {
            StringBuilder html = new StringBuilder();
            int index = 0;
            string fixtureName;
            string fixtureNamespace;
            string fixtureReason;

            // Loop through all of the fixtures
            foreach (var fixture in fixtures)
            {
                // Load fixture details
                fixtureName = fixture.Attribute("name").Value;
                fixtureNamespace = GetElementNamespace(fixture);
                fixtureReason = fixture.Element("reason") != null ? fixture.Element("reason").Element("message").Value : string.Empty;
                html.AppendLine("<div class=\"col-md-3\">");
                html.AppendLine("<div class=\"panel ");

                var fixtureResult = getFixtureStatus(fixture);

                // Colour code panels
                switch (fixtureResult)
                {
                    case FixtureStatus.pass:
                        html.Append("panel-success");
                        break;
                    case FixtureStatus.skip:
                        html.Append("panel-info");
                        break;
                    case FixtureStatus.fail:
                        html.Append("panel-danger");
                        break;
                    default:
                        html.Append("panel-default");
                        break;
                }

                html.Append("\">");
                html.AppendLine("<div class=\"panel-heading\">");
                html.AppendLine(string.Format("{0}<small class=\"pull-right\">{1}</small>", fixtureName, getFixtureSummary(fixture)));

                // If the fixture has a reason, display an icon 
                // on the top of the panel with a tooltip containing 
                // the reason
                if (!string.IsNullOrEmpty(fixtureReason))
                {
                    html.AppendLine(string.Format("<span class=\"glyphicon glyphicon-info-sign pull-right info hidden-print\" data-toggle=\"tooltip\" title=\"{0}\"></span>", fixtureReason));
                }

                html.AppendLine("</div>");
                html.AppendLine("<div class=\"panel-body\">");

                // Generate a unique id for the modal dialog
                string modalId = string.Format("modal-{0}-{1}", Regex.Replace(HttpUtility.UrlEncode(fixtureName), string.Empty), index++);

                html.AppendLine("<div class=\"text-center\" style=\"font-size: 1.5em;\">");

                // Add a colour coded link to the modal dialog
                switch (fixtureResult)
                {
                    case FixtureStatus.pass:
                        html.AppendLine(string.Format("<a href=\"#{0}\" role=\"button\" data-toggle=\"modal\" class=\"text-success no-underline\">", modalId));
                        html.AppendLine("<span class=\"glyphicon glyphicon-ok-sign\"></span>");
                        html.AppendLine("<span class=\"test-result\">Success</span>");
                        html.AppendLine("</a>");
                        break;
                    case FixtureStatus.fail:
                        html.AppendLine(string.Format("<a href=\"#{0}\" role=\"button\" data-toggle=\"modal\" class=\"text-danger no-underline\">", modalId));
                        html.AppendLine("<span class=\"glyphicon glyphicon-exclamation-sign\"></span>");
                        html.AppendLine("<span class=\"test-result\">Failed</span>");
                        html.AppendLine("</a>");
                        break;
                    case FixtureStatus.skip:
                        html.AppendLine(string.Format("<a href=\"#{0}\" role=\"button\" data-toggle=\"modal\" class=\"text-info no-underline\">", modalId));
                        html.AppendLine("<span class=\"glyphicon glyphicon-asterisk\"></span>");
                        html.AppendLine("<span class=\"test-result\">Skipped</span>");
                        html.AppendLine("</a>");
                        break;
                    default:
                        break;
                }

                html.AppendLine("</div>");

                // Generate a printable view of the fixtures
                html.AppendLine(GeneratePrintableView(fixture, fixtureReason));

                // Generate the modal dialog that will be shown
                // if the user clicks on the test-fixtures
                html.AppendLine(GenerateFixtureModal(fixture, modalId, fixtureName, fixtureReason));

                html.AppendLine("</div>");
                html.AppendLine("</div>");
                html.AppendLine("</div>");
            }

            return html.ToString();
        }

        /// <summary>
        /// Determines the fixture status based on all child test statuses
        /// </summary>
        /// <param name="fixture">Fixture to iterate</param>
        /// <returns>FixtureStatus enum: skip if any test case was not executed, fail if a test returned success=false, else pass</returns>
        private static FixtureStatus getFixtureStatus(XElement fixture)
        {
            foreach (var testCase in fixture.Descendants("test-case"))
            {
                var pass = testCase.Attribute("success").Value.ToLower();
                var executed = testCase.Attribute("executed").Value.ToLower();

                if (pass == "false")
                    return FixtureStatus.fail;

                if (executed == "false")
                    return FixtureStatus.skip;
            }

            return FixtureStatus.pass;
        }

        /// <summary>
        /// Returns a summary of the fixture result in the form of Pass/Fail/Skipped/Total
        /// </summary>
        /// <param name="fixture"></param>
        /// <returns>Summary string</returns>
        private static string getFixtureSummary(XElement fixture)
        {
            var p = 0;
            var f = 0;
            var s = 0;

            foreach (var testCase in fixture.Descendants("test-case"))
            {
                var pass = testCase.Attribute("success").Value.ToLower();
                var executed = testCase.Attribute("executed").Value.ToLower();

                if (pass == "false")
                    f++;
                else
                {
                    if (executed == "false")
                        s++;
                    else
                        p++;
                }
            }

            return string.Format("p:{0} / f:{1} / s:{2}", p, f, s);
        }

        private static string GetElementNamespace(XElement element)
        {
            // Move up the tree to get the parent elements
            var namespaces = element.Ancestors("test-suite").Where(x => x.Attribute("type").Value.ToLower() == "namespace");

            // Get the namespace
            return string.Join(".", namespaces.Select(x => x.Attribute("name").Value));
        }

        private static string GeneratePrintableView(XElement fixture, string warningMessage)
        {
            StringBuilder html = new StringBuilder();

            string name, result, executed;
            html.AppendLine("<div class=\"visible-print printed-test-result\">");

            // Display a warning message if set
            if (!string.IsNullOrEmpty(warningMessage))
            {
                html.AppendLine(string.Format("<div class=\"alert alert-warning\"><strong>Warning:</strong> {0}</div>", warningMessage));
            }

            // Loop through test cases in the fixture
            foreach (var testCase in fixture.Descendants("test-case"))
            {
                // Get test case properties
                name = testCase.Attribute("name").Value;
                result = testCase.Attribute("success").Value;
                executed = testCase.Attribute("executed").Value;

                // Remove namespace if in name
                name = name.Substring(name.LastIndexOf('.') + 1, name.Length - name.LastIndexOf('.') - 1);

                // Create colour coded panel based on result
                html.AppendLine("<div class=\"panel ");

                switch (result.ToLower())
                {
                    case "true":
                        html.Append("panel-success");
                        break;
                    case "false":
                        if (executed.ToLower() == "true")
                            html.Append("panel-danger");
                        else
                            html.Append("panel-info");
                        break;
                    default:
                        html.Append("panel-default");
                        break;
                }

                html.Append("\">");

                html.AppendLine("<div class=\"panel-heading\">");
                html.AppendLine("<h4 class=\"panel-title\">");
                html.AppendLine(name);
                html.AppendLine("</h4>");
                html.AppendLine("</div>");
                html.AppendLine("<div class=\"panel-body\">");

                var status = "pass";
                if (result.ToLower() == "false")
                    status = "fail";
                else
                if (executed.ToLower() == "false")
                    status = "skipped";


                html.AppendLine(string.Format("<div><strong>{0}</strong></div>", status));

                // Add failure messages if available
                if (testCase.Elements("failure").Count() == 1)
                {
                    html.AppendLine(string.Format("<div><strong>Message:</strong> {0}</div>", testCase.Element("failure").Element("message").Value));
                    html.AppendLine(string.Format("<div><strong>Stack Trace:</strong> <pre>{0}</pre></div>", testCase.Element("failure").Element("stack-trace").Value));
                }

                html.AppendLine("</div>");
                html.AppendLine("</div>");
            }

            html.AppendLine("</div>");

            return html.ToString();
        }

        private static string GenerateFixtureModal(XElement fixture, string modalId, string title, string warningMessage)
        {
            StringBuilder html = new StringBuilder();

            html.AppendLine(string.Format("<div class=\"modal fade\" id=\"{0}\" tabindex=\"-1\" role=\"dialog\" aria-labelledby=\"myModalLabel\" aria-hidden=\"true\">", modalId));
            html.AppendLine("<div class=\"modal-dialog\">");
            html.AppendLine("<div class=\"modal-content\">");
            html.AppendLine("<div class=\"modal-header\">");
            html.AppendLine("<button type=\"button\" class=\"close\" data-dismiss=\"modal\" aria-hidden=\"true\">&times;</button>");
            html.AppendLine(string.Format("<h4 class=\"modal-title\" id=\"myModalLabel\">{0}</h4>", title));
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"modal-body\">");

            int i = 0;
            string name, result;
            html.AppendLine(string.Format("<div class=\"panel-group no-bottom-margin\" id=\"{0}-accordion\">", modalId));

            if (!string.IsNullOrEmpty(warningMessage))
            {
                html.AppendLine(string.Format("<div class=\"alert alert-warning\"><strong>Warning:</strong> {0}</div>", warningMessage));
            }

            // Add each test case to the dialog, colour 
            // coded based on the result
            foreach (var testCase in fixture.Descendants("test-case"))
            {
                // Get properties
                name = testCase.Attribute("name").Value;
                result = testCase.Attribute("success").Value;
                var executed = testCase.Attribute("executed").Value;
                // Remove namespace if included
                name = name.Substring(name.LastIndexOf('.') + 1, name.Length - name.LastIndexOf('.') - 1);

                html.AppendLine("<div class=\"panel ");

                switch (result.ToLower())
                {
                    case "true":
                        if (executed.ToLower() == "true")
                            html.Append("panel-success");
                        else
                            html.Append("panel-info");
                        break;
                    case "ignored":
                        html.Append("panel-info");
                        break;
                    case "false":
                        if (executed.ToLower() == "true")
                            html.Append("panel-danger");
                        else
                            html.Append("panel-info");
                        break;
                    default:
                        html.Append("panel-default");
                        break;
                }

                html.Append("\">");

                html.AppendLine("<div class=\"panel-heading\">");
                html.AppendLine("<h4 class=\"panel-title\">");
                html.AppendLine(string.Format("<a data-toggle=\"collapse\" data-parent=\"#{1}\" href=\"#{1}-accordion-{2}\">{0}</a>", name, modalId, i));
                html.AppendLine("</h4>");
                html.AppendLine("</div>");
                html.AppendLine(string.Format("<div id=\"{0}-accordion-{1}\" class=\"panel-collapse collapse\">", modalId, i++));
                html.AppendLine("<div class=\"panel-body\">");

                var status = "pass";
                if (result.ToLower() == "false")
                    status = "fail";
                else
                if (executed.ToLower() == "false")
                    status = "skipped";

                html.AppendLine(string.Format("<div><strong>Status:</strong> {0}</div>", status));

                // Add failure messages if available
                if (testCase.Elements("failure").Count() == 1)
                {
                    html.AppendLine(string.Format("<div><strong>Message:</strong> {0}</div>", testCase.Element("failure").Element("message").Value));
                    html.AppendLine(string.Format("<div><strong>Stack Trace:</strong> <pre>{0}</pre></div>", testCase.Element("failure").Element("stack-trace").Value));
                }

                html.AppendLine("</div>");
                html.AppendLine("</div>");
                html.AppendLine("</div>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"modal-footer\">");
            html.AppendLine("<button type=\"button\" class=\"btn btn-primary\" data-dismiss=\"modal\">Close</button>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            return html.ToString();
        }

        private static string GetHTML5Header(string title)
        {
            StringBuilder header = new StringBuilder();
            header.AppendLine("<!doctype html>");
            header.AppendLine("<html lang=\"en\">");
            header.AppendLine("  <head>");
            header.AppendLine("    <meta charset=\"utf-8\">");
            header.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1, maximum-scale=1\" />"); // Set for mobile
            header.AppendLine(string.Format("    <title>{0}</title>", title));

            // Add custom scripts
            header.AppendLine("    <script>");

            // Include jQuery in the page
            header.AppendLine(NunitReporter.Properties.Resources.jQuery);
            header.AppendLine("    </script>");
            header.AppendLine("    <script>");

            // Include Bootstrap in the page
            header.AppendLine(NunitReporter.Properties.Resources.BootstrapJS);
            header.AppendLine("    </script>");
            header.AppendLine("    <script type=\"text/javascript\">");
            header.AppendLine("    $(document).ready(function() { ");
            header.AppendLine("        $('[data-toggle=\"tooltip\"]').tooltip({'placement': 'bottom'});");
            header.AppendLine("    });");
            header.AppendLine("    </script>");

            // Add custom styles
            header.AppendLine("    <style>");

            // Include Bootstrap CSS in the page
            header.AppendLine(NunitReporter.Properties.Resources.BootstrapCSS);
            header.AppendLine("    .page { margin: 15px 0; }");
            header.AppendLine("    .no-bottom-margin { margin-bottom: 0; }");
            header.AppendLine("    .printed-test-result { margin-top: 15px; }");
            header.AppendLine("    .reason-text { margin-top: 15px; }");
            header.AppendLine("    .scroller { overflow: scroll; }");
            header.AppendLine("    @media print { .panel-collapse { display: block !important; } }");
            header.AppendLine("    .val { font-size: 38px; font-weight: bold; margin-top: -10px; }");
            header.AppendLine("    .stat { font-weight: 800; text-transform: uppercase; font-size: 0.85em; color: #6F6F6F; }");
            header.AppendLine("    .test-result { display: block; }");
            header.AppendLine("    .no-underline:hover { text-decoration: none; }");
            header.AppendLine("    .text-default { color: #555; }");
            header.AppendLine("    .text-default:hover { color: #000; }");
            header.AppendLine("    .info { color: #888; }");
            header.AppendLine("    </style>");
            header.AppendLine("  </head>");
            header.AppendLine("  <body>");

            return header.ToString();
        }

        private static string GetHTML5Footer()
        {
            StringBuilder footer = new StringBuilder();
            footer.AppendLine("  </body>");
            footer.AppendLine("</html>");

            return footer.ToString();
        }
    }
}
