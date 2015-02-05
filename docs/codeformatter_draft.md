Code Formatter
===

We have just released the [code formatting tool](https://github.com/dotnet/codeformatter/issues) we use to automatically format our code to the prescribed [coding guidelines](https://github.com/dotnet/corefx/commit/3251c1cae5860785cfb716d3822d11149a88e0fa).  This tool is based on Roslyn and will work on any C# project. 

We strongly believe that having a consistent style greatly increases the readability and maintainability of a code base.  The individual style decisions themselves are subject, the key is the consistency of the decisions.  As such the .Net team has a set of style guidelines for all C# code written by the team.

Unfortunately these style decisions weren't around from the beginning of .Net and as a result a good number of legacy code bases didn't follow them.  A coding style loses its benefits if it's not followed throughout the project and many of these non-conformant code bases represented some of our most core BCL projects.  

This was a long standing issue internally that just never quite met the bar for fixing.  Why waste a developers time editting thousands and thousands of C# files when there are all of these real bugs to fix?  

This changed when the decision was made to open source the code.  The internal team was used to the inconsistencies but did we want to force it on our users?  What would our contribution guidelines look like in a world where some of our most core libraries had a different style than the one we wanted to recommend?  That didn't seem like a great story for customers and we decided to fix it with tooling. 

Luckily the Roslyn project presented us with the tools to fix this problem.  Its core features include a full fidelity parse tree and a set of APIs to safely manipulate it syntactically or semantically.  This is exactly what we needed to migrate all of that legacy source into a single unified style. 

The tool itself is very simple to use; point it at a project or solution and it will systematically convert all of the code involved into the prescribed coding style.  The process is very fast, taking only a few seconds for most projects and up to a couple of minutes for very large ones.  It can even be run repeatedly on them to ensure that a consistent style is maintained over the course of time. 

Right now the tool is a library with a simple command line wrapper.  Going forward we will be looking to support a number of other scenarios including working inside of Visual Studio.  Why should developers ever think about mindless style edits when the tool can just fix up the style differences as you code?  

