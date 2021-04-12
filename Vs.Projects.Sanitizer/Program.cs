using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tooling.Vs.Projects.Sanitizer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter solutionPath:");
            var solutionPath = Console.ReadLine();

            Console.WriteLine("Enter solutionPath:");
            var solutionName = Console.ReadLine();

            foreach (var projectFile in GetFiles($"C:\\Projects\\{solutionPath}\\src", ".csproj"))
            {
                var projectName = Path.GetFileNameWithoutExtension(projectFile);
                var projectDir = Path.GetDirectoryName(projectFile) ?? throw new ArgumentException();
                var projectDirName = new DirectoryInfo(projectDir).Name;

                if (projectName != projectDirName)
                {
                    var newProjectDir = Path.Combine(Directory.GetParent(projectDir)?.FullName ?? string.Empty,
                        projectName);

                    try
                    {
                        Directory.Move(projectDir, newProjectDir);      
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

            var projects = GetFiles($"C:\\Projects\\{solutionPath}\\src", ".csproj")
                .ToList();

            foreach (var projectFile in projects)
            {
                var projectDefinition = XDocument.Load(projectFile);
                
                foreach (var itemGroup in projectDefinition
                    .Element("Project")?
                    .Elements("ItemGroup") ?? Array.Empty<XElement>())
                {
                    foreach (var projectReference in itemGroup.Elements("ProjectReference"))
                    {
                        var include = projectReference.Attribute("Include");
                        if (include == default)
                        {
                            continue;
                        }

                        var simpleProjectName     = string.Join('.', Path.GetFileName(include.Value).Split('.')[^2..]);
                        var longProjectName       = string.Join('.', Path.GetFileName(include.Value).Split('.')[^3..]);
                        var referencedProjectPath = projects.FirstOrDefault(p => p.EndsWith(longProjectName)) ?? projects.FirstOrDefault(p => p.EndsWith(simpleProjectName));

                        if (referencedProjectPath == default)
                        {
                            projectReference.Remove();
                            continue;
                        }

                        var referencedProjectName = Path.GetFileNameWithoutExtension(referencedProjectPath);
                        var referencePath         = $"{referencedProjectName}{Path.DirectorySeparatorChar}{referencedProjectName}.csproj";
                        var solutionDir           = referencedProjectPath.Split(
                                                    Path.DirectorySeparatorChar, 
                                                    StringSplitOptions.RemoveEmptyEntries)[^3];
                        var projectFileSolutionDir    = projectFile.Split(
                            Path.DirectorySeparatorChar, 
                            StringSplitOptions.RemoveEmptyEntries)[^3];

                        var basePath = "..\\";
                        if (solutionDir != projectFileSolutionDir)
                        {
                            basePath = $"..\\..\\{solutionDir}";
                        }
                        
                        var projectFullName =
                            Path.Combine(basePath, referencePath);

                        if (!include.Value.EndsWith(projectFullName))
                        {
                            Console.WriteLine($"Wrong project path found : {include.Value}");
                            
                            include.SetValue(projectFullName);

                            Console.WriteLine($"Changed solution project reference to {include.Value}");
                        }
                    }
                }
                
                projectDefinition.Save(projectFile, SaveOptions.OmitDuplicateNamespaces);
            }

            var slnFile = $"C:\\Projects\\{solutionPath}\\{solutionName}";

            var parser = new SolutionParser(slnFile);
            foreach( var project in parser.GetProjects() )
            {
                var simpleProjectName     = string.Join('.', Path.GetFileName(project.RelativePath).Split('.')[^2..]);
                var longProjectName       = string.Join('.', Path.GetFileName(project.RelativePath).Split('.')[^3..]);
                var referencedProjectPath = projects
                    .FirstOrDefault(p => p.EndsWith(longProjectName)) ?? projects
                    .FirstOrDefault(p => p.EndsWith(simpleProjectName));

                if (referencedProjectPath == default)
                {
                    continue;
                }

                var referencedProjectName  = Path.GetFileNameWithoutExtension(referencedProjectPath);
                var referencePath          = $"{referencedProjectName}{Path.DirectorySeparatorChar}{referencedProjectName}.csproj";

                var projectFileNameSegments = Path.GetFileName(project.ProjectName)?.Split('.').Concat(new []{"csproj"}).ToArray();
                var simpleProjectFileName     = string.Join('.', projectFileNameSegments?[^2..] ?? throw new InvalidOperationException());
                var longProjectFileName       = string.Join('.', projectFileNameSegments.Length > 2 ? projectFileNameSegments[^3..] : projectFileNameSegments[^2..]);
                var projectFileSolutionDir = (projects
                        .FirstOrDefault(p => p.EndsWith(longProjectFileName)) ?? projects
                        .FirstOrDefault(p => p.EndsWith(simpleProjectFileName)))?
                    .Split(Path.DirectorySeparatorChar)[^3];

                var projectFullName = Path.Combine("src", projectFileSolutionDir, referencePath);
                
                if (!project.RelativePath.EndsWith(projectFullName))
                {
                    Console.WriteLine($"Wrong project path found : {project.RelativePath}");

                    project.RelativePath = projectFullName;

                    Console.WriteLine($"Changed solution project reference to {project.RelativePath}");
                }
            }
                
            parser.SaveAs(slnFile);
        }

        public static IEnumerable<string> GetFiles(string path, 
            string searchPatternExpression = "",
            SearchOption searchOption = SearchOption.AllDirectories)
        {
            var reSearchPattern = new Regex(searchPatternExpression, RegexOptions.IgnoreCase);
            return Directory.EnumerateFiles(path, "*", searchOption)
                .Where(file =>
                    reSearchPattern.IsMatch(Path.GetExtension(file)));
        }
    }
}
