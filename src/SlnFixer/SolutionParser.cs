using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Tools.SlnFixer
{
    public class SolutionParser
    {
        private readonly List<object> _slnLines; 

        public SolutionParser( string solutionFileName )
        {
            _slnLines = new List<object>();
            var slnTxt = File.ReadAllText(solutionFileName);
            var projMatcher = new Regex("Project\\(\"(?<ParentProjectGuid>{[A-F0-9-]+})\"\\) = \"(?<ProjectName>.*?)\", \"(?<RelativePath>.*?)\", \"(?<ProjectGuid>{[A-F0-9-]+})");

            Regex.Replace(slnTxt, "^(.*?)[\n\r]*$", match =>
                {
                    var line = match.Groups[1].Value;

                    var m2 = projMatcher.Match(line);
                    if (m2.Groups.Count < 2)
                    {
                        _slnLines.Add(line);
                        return "";
                    }

                    var s = new SolutionProject();
                    foreach (var g in projMatcher.GetGroupNames().Where(x => x != "0")) /* "0" - RegEx special kind of group */
                        s.GetType().GetField(g)?.SetValue(s, m2.Groups[g].ToString());

                    _slnLines.Add(s);
                    return "";
                }, 
                RegexOptions.Multiline
            );
        }
    
        public List<SolutionProject> GetProjects( bool bGetAlsoFolders = false )
        {
            var projects = _slnLines.OfType<SolutionProject>();

            if( !bGetAlsoFolders )
                projects = projects.Where( x => x.RelativePath != x.ProjectName );

            return projects.ToList();
        }
    
        public void SaveAs( string asFilename )
        {
            var s = new StringBuilder();

            for( var i = 0; i < _slnLines.Count; i++ )
            {
                if( _slnLines[i] is string ) 
                    s.Append(_slnLines[i]);
                else
                    s.Append((_slnLines[i] as SolutionProject)?.AsSlnString() );

                if( i != _slnLines.Count )
                    s.AppendLine();
            }

            File.WriteAllText(asFilename, s.ToString());
        }

        public class SolutionProject
        {
            public string ParentProjectGuid;
            public string ProjectName;
            public string RelativePath;
            public string ProjectGuid;

            public string AsSlnString()
            { 
                return "Project(\"" + ParentProjectGuid + "\") = \"" + ProjectName + "\", \"" + RelativePath + "\", \"" + ProjectGuid + "\"";
            }
        }
    }
}