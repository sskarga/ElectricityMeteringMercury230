namespace ServiceEnergyColletor.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Add_report_and_param_tables : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Accounts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FacilityId = c.Int(nullable: false),
                        ReportDate = c.DateTime(nullable: false, storeType: "date"),
                        ReportHour = c.Int(nullable: false),
                        EnergyA = c.Int(nullable: false),
                        EnergyR = c.Int(nullable: false),
                        WorkTime = c.Int(nullable: false),
                        PollCount = c.Int(nullable: false),
                        ErrPoll = c.Int(nullable: false),
                        tsUpdate = c.DateTime(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Facilities", t => t.FacilityId, cascadeDelete: true)
                .Index(t => t.FacilityId);
            
            CreateTable(
                "dbo.Facilities",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        PollChannelId = c.Int(nullable: false),
                        Name = c.String(maxLength: 30),
                        Description = c.String(maxLength: 200),
                        Factor = c.Int(nullable: false),
                        NetworkAddress = c.Byte(nullable: false),
                        Active = c.Boolean(nullable: false),
                        TriggerThresholdEnergy = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.PollChannels", t => t.PollChannelId, cascadeDelete: true)
                .Index(t => t.PollChannelId);
            
            CreateTable(
                "dbo.DailyReports",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FacilityId = c.Int(nullable: false),
                        ReportDate = c.DateTime(nullable: false, storeType: "date"),
                        EnergyABegin = c.Int(nullable: false),
                        EnergyRBegin = c.Int(nullable: false),
                        EnergyAEnd = c.Int(nullable: false),
                        EnergyREnd = c.Int(nullable: false),
                        EnergyAPeak = c.Int(nullable: false),
                        EnergyRPeak = c.Int(nullable: false),
                        WorkTimePeak = c.Int(nullable: false),
                        EnergyAHalfPeak = c.Int(nullable: false),
                        EnergyRHalfPeak = c.Int(nullable: false),
                        WorkTimeHalfPeak = c.Int(nullable: false),
                        EnergyANight = c.Int(nullable: false),
                        EnergyRNight = c.Int(nullable: false),
                        WorkTimeNight = c.Int(nullable: false),
                        PctSuccessfulPolls = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Facilities", t => t.FacilityId, cascadeDelete: true)
                .Index(t => t.FacilityId);
            
            CreateTable(
                "dbo.PollChannels",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 30),
                        TypeChannel = c.String(nullable: false, maxLength: 30),
                        ConnectStrJSON = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Parameters",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        value = c.String(maxLength: 200),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Accounts", "FacilityId", "dbo.Facilities");
            DropForeignKey("dbo.Facilities", "PollChannelId", "dbo.PollChannels");
            DropForeignKey("dbo.DailyReports", "FacilityId", "dbo.Facilities");
            DropIndex("dbo.DailyReports", new[] { "FacilityId" });
            DropIndex("dbo.Facilities", new[] { "PollChannelId" });
            DropIndex("dbo.Accounts", new[] { "FacilityId" });
            DropTable("dbo.Parameters");
            DropTable("dbo.PollChannels");
            DropTable("dbo.DailyReports");
            DropTable("dbo.Facilities");
            DropTable("dbo.Accounts");
        }
    }
}
