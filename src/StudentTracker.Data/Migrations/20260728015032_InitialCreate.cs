using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: false),
                    CompanyLogoPath = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultTrainerName = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultTrainerBusinessDetails = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultAuthorisedByName = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultAuthorisedByPosition = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultVerifiedByName = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultVerifiedByPosition = table.Column<string>(type: "TEXT", nullable: true),
                    SignOffDeclarationText = table.Column<string>(type: "TEXT", nullable: true),
                    BillableTrigger = table.Column<string>(type: "TEXT", nullable: false),
                    ExpenseTrigger = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultCreditPoolId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultBudgetPoolId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DataRootPath = table.Column<string>(type: "TEXT", nullable: false),
                    BackupLocation = table.Column<string>(type: "TEXT", nullable: false),
                    InvoicerExchangeLocation = table.Column<string>(type: "TEXT", nullable: false),
                    ReportFooter = table.Column<string>(type: "TEXT", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    DateFormat = table.Column<string>(type: "TEXT", nullable: false),
                    StudentIdSeed = table.Column<int>(type: "INTEGER", nullable: false),
                    DeliveryIdSeed = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityDisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    OldValuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    NewValuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetPools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    FinancialPeriod = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CertificateCreditPools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    UnitType = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateCreditPools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    CourseCode = table.Column<string>(type: "TEXT", nullable: false),
                    CourseTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultCertificateCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    DefaultCreditQuantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: false),
                    StoredFileName = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", nullable: true),
                    MimeType = table.Column<string>(type: "TEXT", nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: true),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Confidentiality = table.Column<string>(type: "TEXT", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    ExportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundingSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: true),
                    DateReceived = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundingSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportReviewQueues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    SourceFileName = table.Column<string>(type: "TEXT", nullable: false),
                    SourceSheet = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRow = table.Column<int>(type: "INTEGER", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedAction = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedValuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    Issue = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportReviewQueues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutcomeReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReasonType = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresNotes = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutcomeReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    MiddleName = table.Column<string>(type: "TEXT", nullable: true),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    PreferredName = table.Column<string>(type: "TEXT", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Employer = table.Column<string>(type: "TEXT", nullable: true),
                    WorkGroup = table.Column<string>(type: "TEXT", nullable: true),
                    EmployeeNumber = table.Column<string>(type: "TEXT", nullable: true),
                    USI = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    PotentialDuplicate = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    CourseDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DateStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    TrainerName = table.Column<string>(type: "TEXT", nullable: true),
                    TrainerBusinessDetails = table.Column<string>(type: "TEXT", nullable: true),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: true),
                    DeliveryStatus = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseDeliveries_CourseDefinitions_CourseDefinitionId",
                        column: x => x.CourseDefinitionId,
                        principalTable: "CourseDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LinkPurpose = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentLinks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalInvoiceId = table.Column<string>(type: "TEXT", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Customer = table.Column<string>(type: "TEXT", nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    GSTAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    PaymentStatus = table.Column<string>(type: "TEXT", nullable: true),
                    AmountAssignedToStudentTracker = table.Column<decimal>(type: "TEXT", nullable: true),
                    FileDocumentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Documents_FileDocumentId",
                        column: x => x.FileDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    StudentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CourseDeliveryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlaceholderName = table.Column<string>(type: "TEXT", nullable: true),
                    LegacyReference = table.Column<string>(type: "TEXT", nullable: true),
                    AllocatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AllocationStatus = table.Column<string>(type: "TEXT", nullable: false),
                    AttendanceStatus = table.Column<string>(type: "TEXT", nullable: false),
                    OutcomeStatus = table.Column<string>(type: "TEXT", nullable: false),
                    OutcomeDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OutcomeReasonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OutcomeNotes = table.Column<string>(type: "TEXT", nullable: true),
                    CertificateCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    BudgetPoolId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreditPoolId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CashCommitmentStatus = table.Column<string>(type: "TEXT", nullable: false),
                    CreditStatus = table.Column<string>(type: "TEXT", nullable: false),
                    CertificateOrderStatus = table.Column<string>(type: "TEXT", nullable: false),
                    CertificateDeliveryStatus = table.Column<string>(type: "TEXT", nullable: false),
                    IsBillable = table.Column<bool>(type: "INTEGER", nullable: false),
                    BillableDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExportedInBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allocations_BudgetPools_BudgetPoolId",
                        column: x => x.BudgetPoolId,
                        principalTable: "BudgetPools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Allocations_CertificateCreditPools_CreditPoolId",
                        column: x => x.CreditPoolId,
                        principalTable: "CertificateCreditPools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Allocations_CourseDeliveries_CourseDeliveryId",
                        column: x => x.CourseDeliveryId,
                        principalTable: "CourseDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Allocations_OutcomeReasons_OutcomeReasonId",
                        column: x => x.OutcomeReasonId,
                        principalTable: "OutcomeReasons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Allocations_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SignOffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    CourseDeliveryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FileDocumentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TrainerName = table.Column<string>(type: "TEXT", nullable: true),
                    TrainerDetails = table.Column<string>(type: "TEXT", nullable: true),
                    TrainerSignedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AuthorisedByName = table.Column<string>(type: "TEXT", nullable: true),
                    AuthorisedByPosition = table.Column<string>(type: "TEXT", nullable: true),
                    AuthorisedSignedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VerifiedByName = table.Column<string>(type: "TEXT", nullable: true),
                    VerifiedByPosition = table.Column<string>(type: "TEXT", nullable: true),
                    VerifiedSignedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignOffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignOffs_CourseDeliveries_CourseDeliveryId",
                        column: x => x.CourseDeliveryId,
                        principalTable: "CourseDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignOffs_Documents_FileDocumentId",
                        column: x => x.FileDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BudgetTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    PoolId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TransactionType = table.Column<string>(type: "TEXT", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    FundingSourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetTransactions_Allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "Allocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BudgetTransactions_BudgetPools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "BudgetPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BudgetTransactions_FundingSources_FundingSourceId",
                        column: x => x.FundingSourceId,
                        principalTable: "FundingSources",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BudgetTransactions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CertificateCreditTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    PoolId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LinkedTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TransactionType = table.Column<string>(type: "TEXT", nullable: false),
                    TransactionDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalCourseNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalPurchaseReference = table.Column<string>(type: "TEXT", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsReconciled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateCreditTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateCreditTransactions_Allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "Allocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CertificateCreditTransactions_CertificateCreditPools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "CertificateCreditPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CertificateCreditTransactions_CertificateCreditTransactions_LinkedTransactionId",
                        column: x => x.LinkedTransactionId,
                        principalTable: "CertificateCreditTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CertificateCreditTransactions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExportBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExportBatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Rate = table.Column<decimal>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportBatchItems_Allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "Allocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExportBatchItems_ExportBatches_ExportBatchId",
                        column: x => x.ExportBatchId,
                        principalTable: "ExportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignOffParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SignOffId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StudentDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    DeliveryDateText = table.Column<string>(type: "TEXT", nullable: true),
                    ParticipantNote = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Attended = table.Column<bool>(type: "INTEGER", nullable: false),
                    OutcomeText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignOffParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignOffParticipants_Allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "Allocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SignOffParticipants_SignOffs_SignOffId",
                        column: x => x.SignOffId,
                        principalTable: "SignOffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificateOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    AllocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OrderedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalReference = table.Column<string>(type: "TEXT", nullable: true),
                    CreditTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    IsReplacement = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReplacementReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateOrders_Allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "Allocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CertificateOrders_CertificateCreditTransactions_CreditTransactionId",
                        column: x => x.CreditTransactionId,
                        principalTable: "CertificateCreditTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CertificateDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    CertificateOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeliveredDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveryMethod = table.Column<string>(type: "TEXT", nullable: true),
                    DeliveredTo = table.Column<string>(type: "TEXT", nullable: true),
                    RecipientDetails = table.Column<string>(type: "TEXT", nullable: true),
                    EvidenceDocumentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateDeliveries_CertificateOrders_CertificateOrderId",
                        column: x => x.CertificateOrderId,
                        principalTable: "CertificateOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CertificateDeliveries_Documents_EvidenceDocumentId",
                        column: x => x.EvidenceDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_BudgetPoolId",
                table: "Allocations",
                column: "BudgetPoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_CourseDeliveryId",
                table: "Allocations",
                column: "CourseDeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_CreditPoolId",
                table: "Allocations",
                column: "CreditPoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_OutcomeReasonId",
                table: "Allocations",
                column: "OutcomeReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_StudentId_CourseDeliveryId",
                table: "Allocations",
                columns: new[] { "StudentId", "CourseDeliveryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransactions_AllocationId",
                table: "BudgetTransactions",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransactions_FundingSourceId",
                table: "BudgetTransactions",
                column: "FundingSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransactions_InvoiceId",
                table: "BudgetTransactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransactions_PoolId",
                table: "BudgetTransactions",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateCreditTransactions_AllocationId",
                table: "CertificateCreditTransactions",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateCreditTransactions_InvoiceId",
                table: "CertificateCreditTransactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateCreditTransactions_LinkedTransactionId",
                table: "CertificateCreditTransactions",
                column: "LinkedTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateCreditTransactions_PoolId",
                table: "CertificateCreditTransactions",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateDeliveries_CertificateOrderId",
                table: "CertificateDeliveries",
                column: "CertificateOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateDeliveries_EvidenceDocumentId",
                table: "CertificateDeliveries",
                column: "EvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateOrders_AllocationId",
                table: "CertificateOrders",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateOrders_CreditTransactionId",
                table: "CertificateOrders",
                column: "CreditTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseDeliveries_CourseDefinitionId",
                table: "CourseDeliveries",
                column: "CourseDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLinks_DocumentId",
                table: "DocumentLinks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLinks_EntityType_EntityId",
                table: "DocumentLinks",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportBatchItems_AllocationId",
                table: "ExportBatchItems",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportBatchItems_ExportBatchId",
                table: "ExportBatchItems",
                column: "ExportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_FileDocumentId",
                table: "Invoices",
                column: "FileDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SignOffParticipants_AllocationId",
                table: "SignOffParticipants",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SignOffParticipants_SignOffId",
                table: "SignOffParticipants",
                column: "SignOffId");

            migrationBuilder.CreateIndex(
                name: "IX_SignOffs_CourseDeliveryId",
                table: "SignOffs",
                column: "CourseDeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_SignOffs_FileDocumentId",
                table: "SignOffs",
                column: "FileDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BudgetTransactions");

            migrationBuilder.DropTable(
                name: "CertificateDeliveries");

            migrationBuilder.DropTable(
                name: "DocumentLinks");

            migrationBuilder.DropTable(
                name: "ExportBatchItems");

            migrationBuilder.DropTable(
                name: "ImportReviewQueues");

            migrationBuilder.DropTable(
                name: "SignOffParticipants");

            migrationBuilder.DropTable(
                name: "FundingSources");

            migrationBuilder.DropTable(
                name: "CertificateOrders");

            migrationBuilder.DropTable(
                name: "ExportBatches");

            migrationBuilder.DropTable(
                name: "SignOffs");

            migrationBuilder.DropTable(
                name: "CertificateCreditTransactions");

            migrationBuilder.DropTable(
                name: "Allocations");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "BudgetPools");

            migrationBuilder.DropTable(
                name: "CertificateCreditPools");

            migrationBuilder.DropTable(
                name: "CourseDeliveries");

            migrationBuilder.DropTable(
                name: "OutcomeReasons");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "CourseDefinitions");
        }
    }
}
