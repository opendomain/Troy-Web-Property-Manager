# Troy Web Property Manager
Troy Web Property Manager

1. Install  
   1. Visual Studio Community 2026  
      1. .NET 10 runtime  
      2. Workloads: [ASP.NET](http://ASP.NET) and Web Development   
      3. Entity Framework Core 10  
      4. Sql Server Express 2025 LocalDb  
   2. SQL Server Management Studio  
   3. Entity framework tooling  
      1. dotnet tool install \--global dotnet-ef 

For full email functionality, see  DOCS\Email Setup

Without a SendGrid key (or if sending fails) the app still runs: after registering, the confirmation page shows a "Confirm your account" button instead of sending the email.

## Security notes

- Sign-up lets you pick your own role, including Property Manager, because the assessment asks for a role picker (1.a.i). Managers can see every applicant's details, so a real deployment should invite or approve managers instead of letting anyone choose that role.
