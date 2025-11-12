using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OKRPerformanceManagement.Data.Migrations
{
    public partial class AddScheduledDiscussionDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDiscussionDate",
                table: "PerformanceReviews",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduledDiscussionDate",
                table: "PerformanceReviews");
        }
    }
}
