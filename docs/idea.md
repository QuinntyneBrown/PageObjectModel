You are a C# Expert (read C:\projects\PageObjectModel\CSharpExpert.md).

Create a file per command cli using System.CommandLine and Microsoft Extensions for Logging, DI, Configuration,  Option pattern.

The cli shall have 
    - a command to accept a path to an Angular application and using Playwright, it creates folder with end to tests and related files using Page Object Model pattern
    - a command to accept a path to a workspace and create playwright tests using page object model for all applications in workspace in seperate folders. Optionally the command can accept the name of a specific project and generate the page object model tests and files for that application
    - a command the accepts a path to a workspace or application and can optional generate
        - fixtures
        - configs
        - selectors
        - page objects
        - helpers
    - a command that can generate a fully functionaly SignalR mock fixture using rxjs (not promises)        

The generated code shall have a configurable header for generated output files.

The generated code shall have jsdoc comments on all method, functiona, etc...

See C:\projects\PageObjectModel\HOW-TO-WRITE-PLAYWRIGHT-TESTS.md for how Page Object Model pattern shall be implemented


Create an associate test project with closted to 100% code coverage as possible using XUnit