Step 1: Install Required Software
1.Download and Install .NET 6.0 SDK
  -Go to: https://dotnet.microsoft.com/download/dotnet/6.0
  -Download and install the SDK (not just runtime)
  -Verify installation: Open Command Prompt and run dotnet --version
  
2.Install SQL Server
  -Download SQL Server Express: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
  -Or install SQL Server LocalDB (lighter option)
  -During installation, remember the server name (usually (localdb)\MSSQLLocalDB or localhost)
  
3.Install Visual Studio 2022
  -Download from: https://visualstudio.microsoft.com/downloads/
  -Install with "ASP.NET and web development" workload

Step 2: Get the Application Code
1.Clone the Repository
   git clone [YOUR_REPOSITORY_URL]
   cd OKRPerformanceManagement
2.Or Download as ZIP
  -Download the project files
  -Extract to a folder like C:\Projects\OKRPerformanceManagement

Step 3: Database Setup
1. Open SQL Server Management Studio (SSMS)
  -Connect to your SQL Server instance
  -Create a new database called OKRPerformanceManagement
2. Update Connection String
  -Open appsettings.json in the Web project
  -Update the connection string:

 {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OKRPerformanceManagement;Trusted_Connection=true;MultipleActiveResultSets=true"
     }
 }

3. Run Database Migrations
    cd OKRPerformanceManagement.Web
    dotnet ef database update


Step 4: Build and Run the Application
1. Open the Solution
  -Open OKRPerformanceManagement.sln in Visual Studio
  -Or navigate to the Web project folder in terminal

2.Restore Packages
 dotnet restore

3. Build the Application
  dotnet build

4. Run the Application
  dotnet run


Complete System Documentation
  🎯 What is OKR Performance Management?
  OKR (Objectives and Key Results) Performance Management is a system that helps organizations:
      Set and track employee performance goals
      Conduct structured performance reviews
      Manage team objectives and key results
      Track progress and provide feedback
👥 User Roles & Permissions
🔑 Admin Role
What they can do:
      View organization-wide performance data
      Create and manage all employees
      Create and manage OKR templates
      View all performance reviews across the organization
      Access comprehensive analytics and reporting
      Manage user roles and permissions

      
Key Features:
Employee Management: Create, edit, and manage all employee records
Template Management: Create OKR templates for different roles
Organization Analytics: View performance trends across the entire organization
Top Performers: See the best performing employees
Performance Distribution: View rating distribution across the organization

👨‍💼 Manager Role
What they can do:
    Manage their team members
    Create performance reviews for team members
    Review and approve employee OKRs
    View team performance history
    Schedule and conduct performance discussions
    Provide feedback and ratings
    
Key Features:
Team Dashboard: Overview of all team members and their performance
Pending Reviews: See reviews waiting for manager input
Team Performance History: View completed reviews for all team members
Review Management: Create, edit, and finalize performance reviews
Discussion Scheduling: Schedule performance discussions with team members

👤 Employee Role
What they can do:
    View and edit their own OKRs
    Submit self-assessments
    View their performance history
    Participate in performance discussions
    Track their progress on objectives
    
Key Features:
My OKRs: View and edit personal objectives and key results
Performance History: See all completed performance reviews
Self-Assessment: Submit personal performance evaluations
Progress Tracking: Monitor progress on current objectives

�� OKR Structure
Objectives
    High-level goals that employees work towards
    Each objective has a weight (percentage of total performance)
    Objectives are created from templates or custom-made
    
Key Results
Specific, measurable outcomes under each objective
Each key result has:
Target: What needs to be achieved
Measure: How it will be measured
Weight: Importance within the objective
Rating Descriptions: What each rating (1-5) means
Rating System
5-Point Scale: 1 (Needs Improvement) to 5 (Exceeds Expectations)
Three Types of Ratings:
Employee Self-Rating


Manager Rating
Final Rating (after discussion)
🔄 Performance Review Process
1. Review Creation
   Managers create reviews using OKR templates
   Reviews are assigned to specific employees
   Review period is set (start and end dates)
2. Employee Self-Assessment
  Employees review their OKRs
  Provide self-ratings for each key result
Add comments and self-assessment text
3. Manager Review
  Managers review employee self-assessments
  Provide manager ratings and feedback
  Add manager comments
4. Discussion Phase
  Manager and employee discuss ratings
  Resolve any disagreements
  Final ratings are agreed upon
5. Finalization
Review is marked as completed
  Both parties can sign off
  Review becomes part of performance history

📈 Performance Analytics
Individual Analytics
  Personal performance trends over time
  Rating distribution
  Progress on current objectives
  Historical performance data
  Team Analytics (Manager View)
  Team performance overview
  Individual team member progress
  Team average ratings
  Completed vs. pending reviews
  Organization Analytics (Admin View)
  Organization-wide performance metrics
  Top performers identification
  Performance distribution across all employees
  Review completion rates


�� User Interface Features
  Dashboard Views
  Role-specific dashboards with relevant information
  Quick access to common tasks
  Performance summaries and key metrics
  Navigation tailored to user role
  Responsive Design
  Works on desktop, tablet, and mobile devices
  Modern, clean interface
  Easy-to-use navigation
  Data Visualization
  Progress bars for objective completion
  Rating distribution charts
  Performance trend graphs
  Color-coded status indicators
  
🔒 Security Features
  Authentication
  Secure login system
  Password requirements
  Session management
  Authorization
  Role-based access control
  Users can only access their permitted areas
  Secure data isolation between roles
  Data Protection
  Encrypted connections (HTTPS)
  Secure database connections
  Input validation and sanitization
  
📱 Key Pages and Features
  Login Page
  Secure authentication
  Role-based redirection after login
  Dashboard Pages
  Admin Dashboard: Organization overview, employee management
  Manager Dashboard: Team overview, pending reviews
  Employee Dashboard: Personal OKRs, performance history
  OKR Management
  View OKRs: Detailed view of objectives and key results
  Edit OKRs: Modify objectives and key results
  Create Reviews: Set up new performance reviews
  
Review Management
  Review Details: Comprehensive review information
  Rating Interface: Easy-to-use rating system
  Comments System: Structured feedback collection
  Performance History
  Role-specific views of performance data
  Historical tracking of performance over time
  Export capabilities for reporting
  
🛠️ Technical Architecture
  Technology Stack
  Backend: ASP.NET Core 6.0 (C#)
  Frontend: Razor Pages with Bootstrap
  Database: SQL Server with Entity Framework Core
  Authentication: ASP.NET Core Identity
  
  Project Structure
  Models: Data entities and view models
  Controllers: Business logic and request handling
  Views: User interface templates
  Services: Business logic services
  Data: Database context and migrations
  
📋 Common Tasks Guide
  For Admins
  Create New Employee:
  Go to Admin Dashboard → Create Employee
  Fill in employee details
  Assign role and manager
  
  Create OKR Template:
  Go to Admin Dashboard → Manage OKR Templates
  Create template with objectives and key results
  Assign to specific roles
  View Organization Performance:
  Go to Admin Dashboard → My Performance Reviews
  View organization-wide analytics
  
For Managers
  Create Performance Review:
  Go to Manager Dashboard → Create Review
  Select employee and template
  Set review period
  Review Employee OKRs:
  
  Go to Pending Reviews
  Click on review to provide ratings
  Add manager comments
  View Team Performance:
  Go to Manager Dashboard → My Performance Reviews
  See team performance history
  
  For Employees
  View My OKRs:
  Go to Employee Dashboard → My OKRs
  See current objectives and key results
  Submit Self-Assessment:
  Go to My OKRs → Edit OKR
  Provide self-ratings and comments
  Submit for manager review
  View Performance History:
  Go to Employee Dashboard → My Performance Reviews
  See completed performance reviews
  
🚨 Troubleshooting
Common Issues
  Application won't start:
  Check if .NET 6.0 SDK is installed
  Verify database connection string
  Run dotnet restore and dotnet build
  Database connection errors:
  Verify SQL Server is running
  Check connection string in appsettings.json
  Run database migrations
  
  Login issues:
  Use the default accounts provided
  Check if user exists in database
  Verify password requirements
  Permission errors:
  Check user role assignment
  Verify role-based access is working
  Clear browser cache



