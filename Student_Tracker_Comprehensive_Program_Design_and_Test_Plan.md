# Student Tracker
## Comprehensive Program Design, Build Specification, Data-Migration Plan and Testing Plan

**Document status:** Build-ready  
**Application type:** Local, offline, single-user Windows desktop application  
**Primary user:** Alex Gillam  
**Currency:** Australian dollars (AUD)  
**Date format:** DD/MM/YYYY  
**Related application:** Invoicer  
**Implementation intent:** This document may be supplied directly to an AI software-development system with the instruction **“GO — build this application exactly as specified.”**

---

# 1. Mandatory Build Instruction

Build a complete, usable, locally installable **Student Tracker** desktop application.

Do not return only:

- mock-ups;
- schemas;
- pseudocode;
- partial scaffolding;
- a web page;
- a generic student-management template;
- a merged invoicing and student-management system.

The finished deliverable must include:

1. working Windows desktop application;
2. local SQLite database;
3. automatic database creation and migrations;
4. student, course, delivery, certificate-credit and budget workflows;
5. document linking and managed local file storage;
6. course-delivery completion sign-off PDF generation;
7. CSV import and export;
8. PDF reports;
9. Invoicer exchange export/import;
10. audit history;
11. backup and restore;
12. automated tests;
13. sample/seed data;
14. migration support for the supplied historical workbook, credit history and sign-off documents;
15. build, installation and user instructions;
16. a self-contained Windows build or installer.

Where a minor detail is not specified, implement the simplest reliable solution that is consistent with this document.

The application is for one person on one computer. Avoid cloud, enterprise, network and multi-user complexity.

---

# 2. Product Purpose

Student Tracker manages the complete local administrative lifecycle of students undertaking courses, including:

- identifying students with stable IDs;
- creating and scheduling course deliveries;
- allocating students or reserved positions to deliveries;
- recording attendance;
- recording completion, withdrawal and non-completion;
- reserving, consuming, releasing and reallocating certificate credits;
- tracking certificate/completion orders;
- tracking certificates delivered;
- tracking certificate-related billing;
- tracking cash budgets separately from certificate credits;
- tracking where funds or credits came from;
- generating Course Delivery Completion Sign-Off PDFs;
- linking documents to students, courses and deliveries;
- producing standard and filtered reports;
- exchanging billable records and invoice references with Invoicer;
- preserving historical records and audit evidence.

The application must support two equal navigation perspectives:

- **Student perspective:** open a student and see all course history, outcomes, credits, certificates, costs, documents and notes.
- **Course-delivery perspective:** open a delivery and see all participants, attendance, outcomes, credits, certificates, costs, sign-offs and documents.

---

# 3. Project Context and Lessons Learned

The previous application attempted to manage:

- invoices;
- clients;
- a ledger;
- students;
- courses;
- certificate budgets;
- reports.

This caused poor integration, duplicated data and compound statuses that were difficult to interpret.

The replacement must follow these rules:

1. Student Tracker and Invoicer remain separate applications.
2. Course Delivery is the central operational record.
3. Student is a permanent person record.
4. Course Definition and Course Delivery are separate.
5. Certificate credits and cash budgets are separate ledgers.
6. Completion, certificate ordering, certificate delivery and billing are separate events.
7. Structured states replace compound text statuses such as:
   - “withdrawn & not charged”;
   - “cancelled & reallocated”;
   - “completed + cert issued”.
8. Balances are calculated from transactions, not manually overwritten.
9. Documents are first-class linked records.
10. Historical ambiguity is preserved and flagged for review rather than guessed or discarded.
11. Blank and TBC course dates are valid.
12. Reserved positions such as “SCJV PENDING” are valid allocations without a named student.

---

# 4. Scope

## 4.1 Included

- Windows 10/11 desktop application
- Single local user
- SQLite database
- Local managed documents
- Students
- Course definitions
- Course deliveries
- Reserved/placeholder allocations
- Student allocations
- Attendance
- Completion
- Withdrawal
- Non-completion
- Outcome reasons
- Certificate-credit pools
- Certificate-credit top-ups
- Credit allocation
- Credit consumption
- Credit release
- Credit reallocation
- Credit expiry and adjustment
- Certificate orders
- Certificate delivery
- Billable certificate records
- Cash budget pools
- Pending commitments
- Forecast budget balance
- Funding sources
- Invoice references
- Alex personal-fund references
- Course Delivery Completion Sign-Off PDFs
- Historical sign-off storage
- Standard reports
- Filtered/customised reports
- CSV import/export
- PDF export
- Invoicer file exchange
- Backup and restore
- Audit log
- Historical-data migration

## 4.2 Excluded

- Cloud hosting
- Multiple simultaneous users
- Browser application
- Mobile application
- Student portal
- Employer portal
- LMS
- Online assessments
- Online enrolment forms
- Email delivery
- Payment processing
- Full accounting
- General CRM
- Automatic RTO API integration
- Public verification website
- Automatic OCR
- Remote access

---

# 5. Recommended Technology

Use:

- **Language:** C#
- **Runtime:** current supported .NET LTS
- **Desktop UI:** WPF
- **Pattern:** MVVM
- **Database:** SQLite
- **ORM:** Entity Framework Core
- **PDF:** QuestPDF
- **CSV:** CsvHelper
- **Optional XLSX export:** ClosedXML
- **Logging:** Serilog
- **Validation:** FluentValidation or equivalent
- **Testing:** xUnit
- **Installer:** MSIX, WiX or self-contained Windows installer

An alternative stack is acceptable only if it produces an equally reliable self-contained Windows desktop application.

---

# 6. Application Data Location

Default data root:

```text
%LOCALAPPDATA%\StudentTracker\
```

Structure:

```text
StudentTracker/
├── Database/
│   └── student-tracker.db
├── Documents/
│   ├── Students/
│   ├── Courses/
│   ├── CourseDeliveries/
│   ├── SignOffs/
│   ├── Certificates/
│   ├── Invoices/
│   ├── Reports/
│   └── General/
├── Imports/
├── Exports/
├── Integration/
│   ├── InvoicerImport/
│   ├── InvoicerExport/
│   ├── Processed/
│   └── Errors/
├── Backups/
├── Logs/
└── Templates/
```

Files should be stored in the managed document directory. SQLite stores metadata and relationships, not large document BLOBs.

---

# 7. Core Domain Model

## 7.1 Student

A permanent person record.

A student:

- receives one stable Student ID;
- may attend many courses;
- may repeat a course;
- may change email, phone or employer without receiving a new ID;
- may be archived but not silently deleted.

## 7.2 Course Definition

The reusable identity of a course, including:

- code;
- title;
- category;
- provider;
- default certificate cost;
- default certificate-credit requirement.

Examples:

- HLTAID009 — Provide cardiopulmonary resuscitation
- HLTAID011 — Provide First Aid
- PUAFIR306 — Identify, detect and monitor hazardous materials at an incident
- Gas Test
- Course Set — HLTAID011 and HLTAID015

## 7.3 Course Delivery

One scheduled, estimated or proposed instance of a Course Definition.

Dates may be:

- one confirmed date;
- date range;
- estimated;
- TBC;
- blank.

A Course Delivery holds:

- participants;
- placeholders;
- attendance;
- outcomes;
- sign-offs;
- documents;
- certificate status;
- financial information.

## 7.4 Allocation

Links a student or placeholder to a Course Delivery.

Each Allocation has separate state fields for:

- allocation status;
- attendance;
- outcome;
- certificate-credit state;
- certificate-order state;
- certificate-delivery state;
- cash commitment;
- billing.

## 7.5 Certificate Credit

A resource representing a completion/certificate position or monetary credit in the completion system.

It is not cash.

## 7.6 Top-Up

A transaction that adds credit to a Certificate Credit Pool.

## 7.7 Certificate Ordered

A completion/certificate has been submitted or ordered in the external completion system.

This normally consumes the credit.

## 7.8 Certificate Delivered

The certificate has been received and/or supplied to the student, employer or relevant party.

## 7.9 Cash Budget

Actual money available for costs.

## 7.10 Pending Commitment

Money reserved against an active student allocation but not yet treated as actual expenditure.

## 7.11 Forecast Available Budget

```text
Actual Available Budget - Pending Commitments
```

## 7.12 Course Delivery Completion Sign-Off

A signed course-level record confirming delivery and participant attendance/outcomes.

This is not an accredited certificate or Statement of Attainment.

---

# 8. Business Rules

## 8.1 Student Rules

1. Every real student has one permanent Student ID.
2. Student names are not unique identifiers.
3. Potential duplicates are flagged for user review.
4. No automatic merge is permitted.
5. A placeholder allocation may exist without a Student ID.
6. A placeholder can later be replaced with a student without losing its financial or credit history.
7. Archiving a student does not remove historical allocations.

## 8.2 Course Rules

1. A Course Definition may have many Course Deliveries.
2. Delivery dates may be blank or TBC.
3. Do not force a false date into a TBC delivery.
4. A delivery can contain several participants.
5. The same course on a different date is a different Course Delivery.
6. A grouped sign-off may include participant delivery dates that differ.

## 8.3 Outcome Rules

Maintain separate states.

### Allocation status

- Reserved
- Enrolled
- Active
- Transferred
- Withdrawn
- Finalised
- Cancelled

### Attendance status

- Not recorded
- Confirmed
- Attended
- Partially attended
- Did not attend
- Exempt

### Outcome status

- Pending
- Completed
- Not completed
- Withdrawn
- Transferred
- Cancelled
- Historical review required

### Certificate-credit status

- None
- Allocated
- Consumed
- Released
- Reallocated
- Expired
- Unavailable
- Review required

### Certificate-order status

- Not required
- Not ready
- Ready
- Ordered
- Cancelled
- Historical review required

### Certificate-delivery status

- Not applicable
- Awaiting
- Delivered
- Withheld
- Lost
- Review required

### Cash-commitment status

- None
- Pending
- Released
- Spent
- Review required

## 8.4 Withdrawal Rules

A withdrawal requires:

- date;
- structured reason;
- free-text notes where required;
- credit reusable decision;
- cash commitment release decision.

Supported scenarios:

### Withdrawal with reusable position

- allocation becomes Withdrawn;
- credit released;
- pending cash commitment released;
- credit may be allocated to another student.

### Withdrawal too late to reallocate

- allocation becomes Withdrawn;
- reason records insufficient notice;
- certificate credit becomes Consumed or Unavailable;
- cash commitment may be released if no money was actually spent;
- the position cannot be reallocated;
- this is included in credit-loss reporting.

### Transfer

- original allocation remains in history;
- original credit may be released or transferred;
- new allocation is linked to the original.

## 8.5 Non-Completion Rules

1. Reason is mandatory.
2. Non-completion does not equal withdrawal.
3. A certificate credit may still be consumed.
4. Consumed credit does not automatically reduce cash.
5. A consumed position may remain billable.
6. Report separately:
   - non-completion with reusable credit;
   - non-completion with consumed credit.

## 8.6 Certificate-Credit Rules

Certificate-credit balance is transaction-driven.

Transaction types:

- TopUp
- Allocate
- Reserve
- Release
- ReallocateOut
- ReallocateIn
- OrderConsume
- ManualConsume
- Expire
- Adjustment
- Reversal

Rules:

1. Never manually overwrite a calculated balance.
2. Allocate reduces available and increases allocated.
3. OrderConsume reduces allocated and increases consumed.
4. Release returns an unconsumed allocation to available.
5. Reallocation creates linked transactions.
6. Negative available balance is blocked unless an override reason is entered.
7. External provider transaction IDs must be retained.
8. Top-up purchases and spreadsheet top-ups must be reconciled to avoid duplication.

## 8.7 Cash-Budget Rules

Cash is also transaction-driven.

Transaction types:

- FundsAdded
- CommitmentCreated
- CommitmentReleased
- ExpenseRecognised
- Reimbursement
- Adjustment
- Reversal

Rules:

1. A pending allocation may create a commitment.
2. Pending commitments affect forecast, not actual cash.
3. Default expense trigger is course completion.
4. Certificate credit consumption alone does not spend cash.
5. Personal funds must be identifiable.
6. Invoice-funded money must link to an invoice reference.
7. Calculated balances cannot be manually overwritten.

## 8.8 Certificate Rules

1. Completion and order are separate.
2. Order and delivery are separate.
3. Default billable trigger is Certificate Ordered.
4. Settings may change billable trigger to:
   - Ordered;
   - Delivered;
   - Manual approval.
5. Duplicate normal orders are blocked.
6. Replacement orders require a reason.
7. A certificate file may link to:
   - student;
   - allocation;
   - delivery;
   - order;
   - delivery record.

## 8.9 Sign-Off Rules

1. Sign-offs belong to a Course Delivery.
2. Sign-off statuses:
   - Draft;
   - Ready for signature;
   - Signed;
   - Superseded;
   - Archived.
3. Signed sign-offs may be locked.
4. A changed sign-off creates a new version.
5. Sign-off participant rows are snapshots.
6. Historical signed PDFs remain immutable linked documents.
7. Participant notes can record exceptions such as:
   - “Withdrew without enough notice to reallocate position.”

## 8.10 Document Rules

1. One document may link to multiple entities.
2. Removing one link must not delete a file that has other links.
3. Store:
   - original filename;
   - managed filename;
   - path;
   - size;
   - MIME type;
   - SHA-256;
   - category;
   - date;
   - version;
   - confidentiality;
   - status.
4. Detect missing or moved files.
5. Support:
   - active;
   - superseded;
   - archived;
   - missing.
6. Document links may target:
   - student;
   - course;
   - delivery;
   - allocation;
   - order;
   - certificate delivery;
   - sign-off;
   - invoice;
   - credit transaction;
   - budget transaction.

---

# 9. Database Design

Use GUIDs as internal primary keys and readable display IDs for users.

## 9.1 Students

- Id
- DisplayId
- FirstName
- MiddleName
- LastName
- PreferredName
- DateOfBirth
- Email
- Phone
- Employer
- WorkGroup
- EmployeeNumber
- USI
- Notes
- IsActive
- IsArchived
- CreatedAt
- UpdatedAt

Display example:

```text
STU-0001
```

## 9.2 CourseDefinitions

- Id
- DisplayId
- CourseCode
- CourseTitle
- Category
- Description
- Provider
- DefaultCertificateCost
- DefaultCreditQuantity
- IsActive
- Notes
- CreatedAt
- UpdatedAt

## 9.3 CourseDeliveries

- Id
- DisplayId
- CourseDefinitionId
- StartDate nullable
- EndDate nullable
- DateStatus
- Location
- TrainerName
- TrainerBusinessDetails
- Capacity nullable
- DeliveryStatus
- Notes
- CreatedAt
- UpdatedAt

## 9.4 Allocations

- Id
- DisplayId
- StudentId nullable
- CourseDeliveryId
- PlaceholderName nullable
- LegacyReference nullable
- AllocatedAt
- AllocationStatus
- AttendanceStatus
- OutcomeStatus
- OutcomeDate nullable
- OutcomeReasonId nullable
- OutcomeNotes
- CertificateCost
- BudgetPoolId nullable
- CreditPoolId nullable
- CashCommitmentStatus
- CreditStatus
- CertificateOrderStatus
- CertificateDeliveryStatus
- IsBillable
- BillableDate nullable
- CreatedAt
- UpdatedAt

## 9.5 OutcomeReasons

- Id
- ReasonType
- Name
- RequiresNotes
- IsActive
- SortOrder

Seed withdrawal reasons:

- Student request
- Employer request
- Medical
- Scheduling conflict
- No longer employed
- Prerequisite not met
- Transferred
- Duplicate allocation
- Administrative error
- Insufficient notice to reallocate
- Other

Seed non-completion reasons:

- Did not attend
- Left early
- Assessment not completed
- Assessment unsuccessful
- Online learning not completed
- Prerequisite not met
- Medical
- Administrative
- Other

## 9.6 CertificateCreditPools

- Id
- DisplayId
- Name
- Provider
- Description
- UnitType: Monetary or Count
- ExpiryDate nullable
- IsActive
- Notes

## 9.7 CertificateCreditTransactions

- Id
- DisplayId
- PoolId
- AllocationId nullable
- LinkedTransactionId nullable
- TransactionType
- TransactionDateTime
- Amount
- Quantity nullable
- SourceType
- ExternalTransactionId nullable
- ExternalCourseNumber nullable
- ExternalPurchaseReference nullable
- InvoiceId nullable
- Reason
- Notes
- CreatedAt

## 9.8 BudgetPools

- Id
- DisplayId
- Name
- Description
- FinancialPeriod
- IsActive
- Notes

## 9.9 BudgetTransactions

- Id
- DisplayId
- PoolId
- AllocationId nullable
- TransactionType
- TransactionDate
- Amount
- FundingSourceId nullable
- InvoiceId nullable
- Reason
- Notes
- CreatedAt

## 9.10 FundingSources

- Id
- DisplayId
- Type:
  - Invoice
  - Company
  - AlexPersonal
  - Reimbursement
  - Grant
  - Other
- Name
- Amount nullable
- DateReceived nullable
- Notes
- IsActive

## 9.11 Invoices

- Id
- DisplayId
- ExternalInvoiceId
- InvoiceNumber
- Customer
- InvoiceDate
- DueDate
- TotalAmount
- GSTAmount
- PaymentStatus
- AmountAssignedToStudentTracker
- FileDocumentId
- Notes

## 9.12 CertificateOrders

- Id
- DisplayId
- AllocationId
- OrderBatchId nullable
- OrderedDate
- Provider
- ExternalReference
- CreditTransactionId
- Quantity
- Notes
- Status
- CreatedAt

## 9.13 CertificateDeliveries

- Id
- DisplayId
- CertificateOrderId
- DeliveredDate
- DeliveryMethod
- DeliveredTo
- RecipientDetails
- EvidenceDocumentId
- Notes
- CreatedAt

## 9.14 SignOffs

- Id
- DisplayId
- CourseDeliveryId
- Version
- Status
- GeneratedDate
- LockedDate nullable
- FileDocumentId
- TrainerName
- TrainerDetails
- TrainerSignedDate nullable
- AuthorisedByName
- AuthorisedByPosition
- AuthorisedSignedDate nullable
- VerifiedByName
- VerifiedByPosition
- VerifiedSignedDate nullable
- Notes

## 9.15 SignOffParticipants

Snapshot fields:

- Id
- SignOffId
- AllocationId nullable
- StudentDisplayName
- DeliveryDateText
- ParticipantNote
- SortOrder

## 9.16 Documents

- Id
- DisplayId
- OriginalFileName
- StoredFileName
- RelativePath
- Extension
- MimeType
- FileSize
- Sha256
- CategoryId
- DisplayName
- Description
- Version
- Status
- Confidentiality
- UploadedAt
- ReceivedDate
- Notes

## 9.17 DocumentLinks

- Id
- DocumentId
- EntityType
- EntityId
- LinkPurpose
- CreatedAt

## 9.18 AuditLog

- Id
- Timestamp
- Action
- EntityType
- EntityId
- EntityDisplayId
- OldValuesJson
- NewValuesJson
- Reason

## 9.19 Settings

Settings include:

- company name;
- company logo;
- trainer default;
- trainer ABN/provider text;
- authorised-by default;
- verified-by default;
- positions;
- optional signature images;
- sign-off declaration text;
- billable trigger;
- expense trigger;
- default credit pool;
- default budget pool;
- data location;
- backup location;
- Invoicer exchange location;
- report footer;
- currency;
- date format.

---

# 10. Calculations

## 10.1 Certificate-Credit Pool

```text
Loaded =
    TopUp
  + positive Adjustments

Allocated =
    active Allocate/Reserve transactions
  - released allocations
  - consumed allocations

Consumed =
    OrderConsume
  + ManualConsume

Expired =
    Expire

Available =
    Loaded
  + net Adjustments
  - active Allocated
  - Consumed
  - Expired
```

Reallocation within one pool does not change the total available pool balance but must show linked history.

## 10.2 Cash Budget

```text
Funds Added =
    FundsAdded
  + positive Adjustments

Actual Expenditure =
    ExpenseRecognised

Pending Commitments =
    active CommitmentCreated
  - CommitmentReleased
  - commitments converted into actual expenditure

Actual Available =
    Funds Added
  - Actual Expenditure

Forecast Available =
    Actual Available
  - Pending Commitments
```

Never label one value simply “Remaining” without indicating actual or forecast.

---

# 11. User Interface

Use a compact desktop interface with searchable grids and detail views.

Colours:

- green — completed, delivered or available;
- blue — enrolled or active;
- amber — pending, scheduled or review;
- red — exception, negative or overdue;
- grey — withdrawn, archived or inactive.

Always show text as well as colour.

## 11.1 Main Navigation

- Dashboard
- Students
- Courses
- Deliveries
- Certificates
- Credits
- Budgets
- Documents
- Reports
- Imports/Exports
- Audit
- Settings

## 11.2 Dashboard

Show:

- active students;
- active deliveries;
- students allocated;
- awaiting outcome;
- completed;
- withdrawn;
- non-completed;
- certificates awaiting order;
- certificates ordered;
- certificates awaiting delivery;
- certificates delivered;
- credits available;
- credits allocated;
- credits consumed;
- credits released;
- credits consumed without completion;
- actual budget remaining;
- pending commitments;
- forecast remaining;
- TBC deliveries;
- missing documents;
- recent activity.

Filters:

- date range;
- course;
- delivery;
- credit pool;
- budget pool;
- status.

## 11.3 Student Register

Columns:

- Student ID
- First Name
- Last Name
- Email
- Phone
- Employer/Work Group
- Active Allocations
- Completed Courses
- Certificates Outstanding
- Notes indicator

Actions:

- Add
- Open
- Edit
- Archive
- Restore
- Duplicate review
- Export
- Attach document

## 11.4 Student Detail

Tabs:

- Overview
- Courses
- Certificates
- Credits
- Financial
- Documents
- Notes
- History

## 11.5 Course Definitions

Columns:

- Course Code
- Course Title
- Category
- Default Certificate Cost
- Default Credits
- Provider
- Active
- Delivery Count

## 11.6 Course Deliveries

Columns:

- Delivery ID
- Course Code
- Course Title
- Date/Date Status
- Location
- Trainer
- Capacity
- Allocated
- Completed
- Withdrawn
- Certificate Status
- Sign-Off Status
- Delivery Status

Actions:

- Create
- Open
- Edit
- Duplicate
- Cancel
- Archive
- Generate Sign-Off
- Export Participant List

## 11.7 Course Delivery Detail

Tabs:

- Overview
- Participants
- Attendance
- Outcomes
- Certificates
- Financial
- Documents
- Sign-Offs
- Reports
- History

Participant columns:

- Student ID
- Student Name
- Legacy Reference
- Attendance
- Outcome
- Reason
- Credit Status
- Certificate Ordered
- Certificate Delivered
- Cost
- Budget Pool
- Notes

Actions:

- Add student
- Add placeholder
- Replace placeholder
- Transfer
- Withdraw
- Mark attendance
- Mark completed
- Mark non-completed
- Allocate credit
- Release credit
- Reallocate credit
- Order certificate
- Record delivery
- Attach document

## 11.8 Certificate Workspace

Sections:

- Awaiting Order
- Ordered
- Awaiting Delivery
- Delivered
- Exceptions
- Consumed Without Completion
- Export to Invoicer

## 11.9 Credits

Show pool cards:

- loaded;
- available;
- allocated;
- consumed;
- released;
- expired;
- unavailable.

Transaction grid:

- ID
- date/time
- type
- amount
- quantity
- student
- course
- allocation
- external transaction
- notes

## 11.10 Budgets

Show:

- funds added;
- actual expenditure;
- pending commitments;
- actual available;
- forecast available.

## 11.11 Documents

Global filters:

- category;
- student;
- course;
- delivery;
- date;
- file type;
- status;
- confidentiality.

## 11.12 Reports

Every report screen provides:

- filters;
- preview;
- CSV export;
- PDF export;
- generated date;
- filter summary;
- page numbers;
- totals.

---

# 12. Required Workflows

## 12.1 Create Student

1. Add Student.
2. Enter first name and last name.
3. Enter optional details.
4. Run duplicate check.
5. Confirm.
6. Assign Student ID.
7. Create audit entry.

## 12.2 Create Course Delivery

1. Select/create Course Definition.
2. Select date status.
3. Enter dates only if known.
4. Add trainer/location/capacity.
5. Save.
6. Assign Delivery ID.

## 12.3 Allocate Student

1. Open delivery.
2. Select student.
3. Enter cost.
4. Select credit and budget pool.
5. Optionally create credit reservation.
6. Optionally create cash commitment.
7. Save all changes in one database transaction.
8. Roll back everything if one part fails.

## 12.4 Create Placeholder

1. Add Placeholder.
2. Enter name such as SCJV PENDING.
3. Enter legacy reference such as SCJV 10.
4. Reserve credit or cash.
5. Later replace with a student while preserving Allocation ID and transaction history.

## 12.5 Mark Completion

1. Confirm attendance.
2. Set outcome Completed.
3. Enter completion date.
4. Convert cash commitment to expense if configured.
5. Do not automatically order a certificate.
6. Audit the change.

## 12.6 Withdraw and Release

1. Select Withdraw.
2. Enter date/reason.
3. Confirm credit reusable.
4. Confirm cash release.
5. Create release transactions.
6. Keep withdrawn allocation visible.

## 12.7 Withdraw Without Reallocation

1. Select reason “Insufficient notice to reallocate”.
2. Set credit reusable to No.
3. Credit becomes Unavailable/Consumed.
4. Cash is handled independently.
5. Add participant note to sign-off if required.

## 12.8 Reallocate Credit

1. Select eligible source allocation.
2. Select destination allocation.
3. Create linked ReallocateOut/ReallocateIn.
4. Retain full history.
5. Do not delete source allocation.

## 12.9 Order Certificate

1. Allocation normally must be Completed.
2. Allow authorised override with reason.
3. Credit must be allocated.
4. Record order date/provider/reference.
5. Consume credit.
6. Create billable item if trigger is Ordered.
7. Block duplicate order.

## 12.10 Deliver Certificate

1. Select ordered certificate.
2. Enter date/method/recipient.
3. Attach certificate/evidence.
4. Mark Delivered.
5. Create billable item if trigger is Delivered.

## 12.11 Generate Sign-Off

1. Open Course Delivery.
2. Select participant rows.
3. Review dates and notes.
4. Pre-populate signatories.
5. Generate Draft PDF.
6. Save as linked Document.
7. Link to delivery, allocations and students.
8. Permit manual signatures or signature images.
9. Lock signed version.
10. Regeneration creates a new version.

## 12.12 Export to Invoicer

1. Select billable records.
2. Generate CSV and JSON.
3. Include stable IDs.
4. Store export batch.
5. Prevent duplicate export.
6. Re-export requires confirmation/reason.

---

# 13. Course Delivery Completion Sign-Off PDF

Title:

**Course Delivery Completion Sign-Off**

Declaration:

> This document serves as a record of training delivery and participant attendance. It confirms that the course listed below was delivered by a suitably qualified trainer on the dates specified, and that the participants named attended the training session(s). The trainer and authorised representative acknowledge that the information contained within this record is accurate and has been completed in accordance with organisational training and record-keeping requirements.

Fields:

- Course delivered
- Trainer
- Trainer business/provider details
- Participant table:
  - Date delivered
  - Participant name
  - Participant note
- Trainer Declaration
- Trainer name
- Trainer signature
- Trainer date
- Authorised By (for SCJV):
  - name
  - position
  - signature
  - date
- Verified By (Town and Country):
  - name
  - position
  - signature
  - date
- Document ID
- Version
- Generated date
- Page number

The visual style should be based on the supplied historical sign-offs.

---

# 14. Reports

Mandatory reports:

1. Student Course History
2. Course Delivery Participant List
3. Course Delivery Outcomes
4. Completed Students
5. Withdrawn Students
6. Withdrawn Students with Costs
7. Withdrawn Students without Costs
8. Non-Completions
9. Credits Consumed Without Completion
10. Certificates Awaiting Order
11. Certificates Ordered
12. Certificates Awaiting Delivery
13. Certificates Delivered
14. Certificate Credit Pool Summary
15. Credit Transaction History
16. Credit Reallocation History
17. Budget Summary
18. Pending Commitments
19. Actual vs Forecast Budget
20. Funding Sources
21. Invoice Reconciliation
22. Missing Documents
23. TBC Course Deliveries
24. Billable Certificates for Invoicer
25. Audit Activity

Filters:

- date;
- student;
- course;
- delivery;
- employer/work group;
- outcome;
- certificate status;
- reason;
- pool;
- costs included/excluded.

---

# 15. Import and Migration Design

The accompanying workbook:

**Student_Tracker_Complete_Migration_Package.xlsx**

is the required migration source.

It contains:

- untouched original workbook data;
- untouched provider credit-history data;
- normalized students;
- course definitions;
- course deliveries;
- allocations;
- credit transactions;
- legacy top-ups requiring reconciliation;
- historical sign-offs;
- sign-off participant snapshots;
- document metadata;
- document links;
- import-review queue;
- target data dictionary.

## 15.1 Data-Preservation Rules

1. Never delete source rows during migration.
2. Preserve:
   - source filename;
   - source row;
   - external transaction ID;
   - external course number;
   - purchase reference;
   - original notes;
   - raw course text;
   - legacy SCJV references.
3. Import uncertain records as review-required staging records.
4. Do not infer positive cost as confirmed completion.
5. Do not duplicate legacy top-ups that match provider credit purchases.
6. Keep blank course dates as TBC.
7. Keep SCJV PENDING as placeholders.
8. Import historical PDFs as immutable documents.
9. Store sign-off participant names as snapshots even when matched to a Student ID.
10. Produce a post-import reconciliation report.

## 15.2 Import Order

1. Settings and seed reasons
2. Students
3. Course Definitions
4. Course Deliveries
5. Allocations
6. Certificate Credit Pool
7. Credit Transactions
8. Historical Sign-Offs
9. Sign-Off Participants
10. Documents
11. Document Links
12. Review Queue
13. Reconciliation

## 15.3 Historical Inference

Suggested staging only:

- note contains “withdrawn” → proposed Withdrawn;
- note contains “cancelled” → proposed Cancelled;
- note contains “no refund” → credit proposed Unavailable;
- SCJV PENDING → placeholder;
- blank date → TBC;
- zero cost → review;
- positive cost with no explicit evidence → historical review.

The user must be able to approve or change inferred values.

## 15.4 Credit Reconciliation

Provider credit-history transactions include:

- credit purchases;
- course/order debits;
- external transaction IDs;
- external course numbers;
- quantities;
- course details.

Match transactions to allocations using:

1. course code;
2. quantity;
3. approximate date;
4. certificate cost;
5. historical notes;
6. sign-off participant evidence.

Never force a match where evidence is insufficient.

---

# 16. Invoicer Integration

Version 1 uses exchange files.

## 16.1 Imported from Invoicer

- External Invoice ID
- Invoice Number
- Customer
- Invoice Date
- Total
- Payment Status
- Amount assigned to Student Tracker
- PDF path

## 16.2 Exported to Invoicer

- Export Batch ID
- Allocation ID
- Student ID
- Student Name
- Course Code
- Course Delivery ID
- Delivery Date
- Certificate Order ID
- Ordered Date
- Delivered Date
- Billable Trigger
- Quantity
- Rate
- Amount
- Notes

Student Tracker is authoritative for students, courses, outcomes, certificates and credits.

Invoicer is authoritative for invoices.

---

# 17. Backup, Restore and Integrity

Required:

- manual backup;
- daily automatic backup;
- pre-import backup;
- pre-upgrade backup;
- pre-restore backup;
- integrity check;
- retention configuration.

Default retention:

- seven daily;
- eight weekly;
- twelve monthly.

Backup includes:

- SQLite database;
- documents;
- settings;
- templates.

Restore procedure:

1. select backup;
2. verify package;
3. warn user;
4. back up current state;
5. restore;
6. run SQLite integrity check;
7. validate document paths;
8. record audit entry.

---

# 18. Audit and Logging

Audit:

- student creation/edit/archive;
- course edits;
- delivery creation/edit/cancel;
- allocation changes;
- attendance;
- completion;
- withdrawal;
- non-completion;
- credit transactions;
- budget transactions;
- certificate orders;
- certificate deliveries;
- sign-off generation/lock;
- imports;
- exports;
- settings;
- restore.

Technical logs:

- startup;
- database migration;
- errors;
- import errors;
- PDF-generation errors;
- file errors;
- backup errors.

Never silently swallow errors.

Multi-step workflows must use database transactions.

---

# 19. Non-Functional Requirements

- Application startup under five seconds on a normal Windows PC.
- Search response under one second for normal datasets.
- Grids load under two seconds with 10,000 allocations.
- SQLite foreign keys enabled.
- All migrations repeatable and versioned.
- No internet required.
- Application continues to function when OneDrive is unavailable.
- Graceful handling of missing files.
- Clear error messages with technical details in logs.
- No direct balance editing.
- Automatic backup status displayed.
- Local data root configurable.
- UI usable at 100%, 125% and 150% Windows scaling.
- Reports use Australian date and currency formatting.
- Export files use UTF-8.

---

# 20. Delivery Phases

## Phase 1 — Foundation

Build:

- solution structure;
- database;
- migrations;
- settings;
- logging;
- backup framework;
- navigation;
- common controls.

Exit criteria:

- application runs;
- database creates;
- migrations apply;
- settings save;
- backup can be created.

## Phase 2 — Students and Courses

Build:

- students;
- duplicate review;
- course definitions;
- course deliveries;
- student and delivery search;
- allocations;
- placeholders.

Exit criteria:

- student can be allocated;
- placeholder can be created and replaced;
- student and course views agree.

## Phase 3 — Outcomes

Build:

- attendance;
- completion;
- withdrawal;
- non-completion;
- reasons;
- history.

Exit criteria:

- all outcome workflows work without compound statuses.

## Phase 4 — Credits

Build:

- pools;
- top-ups;
- allocations;
- release;
- consumption;
- reallocation;
- external transaction references.

Exit criteria:

- balance reconciles from transactions;
- no double allocation;
- no silent negative balance.

## Phase 5 — Budgets

Build:

- pools;
- funding sources;
- commitments;
- releases;
- expenditure;
- actual and forecast balances.

Exit criteria:

- certificate credit and cash effects remain independent.

## Phase 6 — Certificates and Sign-Offs

Build:

- order;
- delivery;
- billable items;
- sign-off PDF;
- signatories;
- PDF versioning.

Exit criteria:

- historical sign-off layout reproduced;
- order and delivery remain separate.

## Phase 7 — Documents and Reports

Build:

- managed file storage;
- links;
- checksums;
- missing-file detection;
- standard reports;
- PDF and CSV export.

## Phase 8 — Migration and Invoicer

Build:

- import wizard;
- migration package reader;
- reconciliation;
- Invoicer exchange;
- export-batch controls.

## Phase 9 — Production Release

Complete:

- automated tests;
- migration trial;
- user acceptance;
- installer;
- user guide;
- backup/restore test;
- release notes.

---

# 21. Comprehensive Testing Plan

## 21.1 Testing Objectives

Confirm that:

1. all required workflows function;
2. balances remain accurate;
3. credits and cash remain separate;
4. historical data imports without loss;
5. documents remain linked;
6. status changes are auditable;
7. PDFs are accurate;
8. backup and restore are reliable;
9. invalid operations are blocked;
10. user can operate the application without technical knowledge.

## 21.2 Test Levels

- Unit tests
- Database tests
- Service/integration tests
- UI workflow tests
- Import/migration tests
- PDF tests
- Backup/restore tests
- Performance tests
- User acceptance tests
- Regression tests

## 21.3 Test Data

Include:

- real-shaped anonymised sample data;
- one student with several courses;
- same student repeating one course;
- TBC delivery;
- placeholder allocation;
- withdrawal with reusable credit;
- withdrawal without reusable credit;
- non-completion with consumed credit;
- completed but not ordered;
- ordered but not delivered;
- delivered certificate;
- duplicate student;
- duplicate order attempt;
- insufficient credit;
- insufficient budget;
- missing document;
- legacy provider transaction;
- historical sign-off.

---

# 22. Unit Test Catalogue

## 22.1 Student

- Create valid student.
- Reject missing required name fields.
- Generate unique display ID.
- Flag likely duplicate by name/email.
- Do not automatically merge duplicates.
- Archive and restore.
- Preserve historical allocations after archive.

## 22.2 Course

- Create course definition.
- Create confirmed delivery.
- Create TBC delivery.
- Create blank-date delivery.
- Allow repeated course on different dates.
- Prevent invalid end date before start date.
- Cancel delivery without deleting participants.

## 22.3 Allocation

- Allocate existing student.
- Allocate placeholder.
- Replace placeholder with student.
- Prevent accidental duplicate allocation to same delivery.
- Transfer allocation.
- Roll back allocation when credit reservation fails.
- Roll back allocation when budget commitment fails.

## 22.4 Outcomes

- Mark attendance.
- Mark completed.
- Mark non-completed.
- Withdraw with reason.
- Reject withdrawal without reason.
- Require notes for configured reason.
- Preserve outcome history.
- Prevent impossible transition unless overridden.

## 22.5 Credits

- Add top-up.
- Calculate available balance.
- Allocate credit.
- Block over-allocation.
- Release credit.
- Consume on order.
- Reallocate.
- Expire credit.
- Reverse transaction.
- Prevent direct calculated-balance edit.
- Recalculate after reversal.

## 22.6 Budgets

- Add funds.
- Create commitment.
- Calculate actual available.
- Calculate forecast available.
- Release commitment.
- Convert commitment to expense.
- Keep cash unchanged when credit alone is consumed.
- Reverse expense.
- Prevent invalid negative balance unless override is supported.

## 22.7 Certificates

- Complete without ordering.
- Order valid certificate.
- Consume allocated credit.
- Block duplicate order.
- Allow replacement with reason.
- Deliver ordered certificate.
- Reject delivery without order unless override.
- Create billable item under Ordered trigger.
- Create billable item under Delivered trigger.
- Do not create duplicate billing item.

## 22.8 Documents

- Add document.
- Generate checksum.
- Link to student.
- Link same document to delivery.
- Remove one link without deleting file.
- Detect missing file.
- Supersede version.
- Archive document.
- Open linked file.

## 22.9 Sign-Off

- Generate draft.
- Include correct participants.
- Include participant notes.
- Include mixed delivery dates.
- Pre-populate signatories.
- Save PDF.
- Link PDF to entities.
- Lock signed version.
- Generate new version after locked record.
- Preserve snapshot when student data later changes.

---

# 23. Workflow Test Scenarios

## WF-001 Successful Completion

1. Create student.
2. Create delivery.
3. Allocate student.
4. Reserve certificate credit.
5. Create cash commitment.
6. Mark attended.
7. Mark completed.
8. Confirm actual expenditure.
9. Order certificate.
10. Deliver certificate.
11. Export billing item.

Expected:

- one student;
- one allocation;
- credit consumed once;
- cash expense once;
- order and delivery dates separate;
- one billable item;
- complete audit trail.

## WF-002 Withdrawal with Reallocation

1. Allocate Student A.
2. Reserve credit and cash.
3. Withdraw Student A.
4. Release credit and cash.
5. Allocate Student B.
6. Reallocate released credit.

Expected:

- Student A remains Withdrawn;
- reason retained;
- Student B receives credit;
- pool total unchanged;
- linked reallocation history;
- no duplicate credit.

## WF-003 Withdrawal Too Late

1. Allocate student.
2. Reserve credit.
3. Withdraw using insufficient-notice reason.
4. Set credit not reusable.
5. Release cash commitment.

Expected:

- outcome Withdrawn;
- credit Unavailable/Consumed;
- cash remains available;
- credit cannot be allocated again;
- report includes consumed-without-completion.

## WF-004 Non-Completion with Consumed Credit

1. Allocate.
2. Reserve credit.
3. Mark non-completed.
4. Consume credit manually or through external order.
5. Do not recognise cash expense.

Expected:

- non-completed;
- credit consumed;
- cash unchanged;
- reason mandatory;
- billable status available according to rule.

## WF-005 Placeholder Replacement

1. Create SCJV PENDING placeholder.
2. Reserve $20 credit.
3. Later replace with named student.
4. Complete course.

Expected:

- same Allocation ID;
- original legacy reference retained;
- no duplicate reservation;
- student history shows allocation.

## WF-006 TBC Delivery

1. Create delivery with Date Status TBC.
2. Allocate students.
3. Later confirm date.
4. Generate sign-off.

Expected:

- no fake date created;
- updated date visible in history;
- allocations preserved.

## WF-007 Course Sign-Off

1. Add five participants with two delivery dates.
2. One participant withdraws too late.
3. Generate sign-off.
4. Review participant note.
5. Add signatures.
6. Lock.

Expected:

- correct course;
- all selected participants;
- correct dates;
- exception note;
- three signatory blocks;
- linked PDF;
- immutable signed version.

## WF-008 Duplicate Certificate Order

1. Order certificate.
2. Attempt second normal order.

Expected:

- blocked;
- no second credit consumption;
- replacement flow requires reason.

## WF-009 Credit Purchase Reconciliation

1. Import provider credit history.
2. Import legacy top-ups.
3. Match duplicate amount/date.
4. Mark legacy record as represented by provider transaction.

Expected:

- only one top-up affects balance;
- both source records remain in evidence/history;
- reconciliation record explains decision.

## WF-010 Invoicer Export

1. Select unexported billable items.
2. Export CSV/JSON.
3. Export same items again.

Expected:

- first batch created;
- second attempt warns/blocks;
- controlled re-export requires reason.

---

# 24. Migration Tests

## MIG-001 Source Preservation

Verify raw original workbook values are preserved.

## MIG-002 Credit History Preservation

Verify all external transaction IDs, dates, credits, debits and descriptions are preserved.

## MIG-003 Student Count

Compare unique staged students to imported students.

## MIG-004 Course Count

Compare course definitions and deliveries.

## MIG-005 Allocation Count

Every source register row must be represented as:

- allocation;
- placeholder;
- or explicit import-review record.

## MIG-006 Blank Dates

Verify blank dates become TBC, not null errors or fabricated dates.

## MIG-007 Legacy References

Verify SCJV references are retained.

## MIG-008 Notes

Verify notes are retained verbatim.

## MIG-009 Sign-Offs

Verify both historical PDFs are imported and linked.

## MIG-010 Participant Snapshots

Verify all historical participant names, dates and notes are preserved.

## MIG-011 Credit Duplicates

Verify legacy spreadsheet top-ups are not double-counted against provider purchases.

## MIG-012 Reconciliation Totals

Calculate:

- total provider credits;
- total provider debits;
- provider balance;
- imported transaction totals.

They must match after approved reconciliation.

## MIG-013 Review Queue

Every uncertain record must appear in review queue.

## MIG-014 Rollback

If migration fails midway, database remains unchanged.

---

# 25. PDF and Report Tests

- Sign-off title correct.
- Declaration correct.
- Long participant names wrap.
- Notes wrap without clipping.
- Multiple pages show repeated table header.
- Signatory blocks do not overlap.
- Page numbers correct.
- PDF opens in standard reader.
- Generated document linked to correct records.
- Report totals match database queries.
- Currency format is AUD.
- Dates are DD/MM/YYYY.
- Filters shown in report header.
- CSV export uses UTF-8.
- No hidden rows omitted unintentionally.

---

# 26. Backup and Restore Tests

- Manual backup created.
- Daily backup created.
- Pre-import backup created.
- Backup contains database and documents.
- Restore creates safety backup first.
- Restored database passes integrity check.
- Documents reopen after restore.
- Audit records restore.
- Current app version handles restored schema.
- Corrupt backup rejected.
- Missing document warning displayed.

---

# 27. Performance Tests

Dataset:

- 10,000 students;
- 20,000 deliveries;
- 100,000 allocations;
- 200,000 transactions.

Targets:

- startup under five seconds for normal production dataset;
- student search under one second;
- delivery list under two seconds;
- report preview under five seconds for common filters;
- CSV export of 100,000 rows completes without crash;
- backup completes with progress indication.

Enterprise-scale optimisation is not required, but normal local use must be responsive.

---

# 28. User Acceptance Tests

The application is accepted when Alex can:

1. import supplied historical data;
2. open any student and see course history;
3. open any delivery and see participants;
4. create a TBC delivery;
5. create a placeholder;
6. replace placeholder with student;
7. withdraw and release credit;
8. withdraw without releasing credit;
9. reallocate a credit;
10. record completion;
11. order a certificate;
12. deliver a certificate;
13. see credits available/allocated/consumed;
14. see actual and forecast budget;
15. link documents;
16. generate sign-off matching historical format;
17. run withdrawal report;
18. run certificates-awaiting-order report;
19. export billable records to Invoicer;
20. back up and restore.

---

# 29. Release Acceptance Criteria

The production version must satisfy all of the following:

- No critical or high-severity unresolved defects.
- Automated unit tests pass.
- Workflow tests pass.
- Migration trial completes.
- Imported totals reconcile.
- Historical files are accessible.
- Sign-off PDFs render correctly.
- Installer works on a clean Windows environment.
- Application can be uninstalled without deleting user data.
- Backup and restore pass.
- User guide exists.
- Database migrations are versioned.
- No requirement depends on internet access.

---

# 30. Required Project Deliverables

The AI/development system must return:

```text
StudentTracker/
├── src/
├── tests/
├── database/
├── templates/
├── sample-data/
├── migration/
├── installer/
├── docs/
│   ├── README.md
│   ├── BUILD.md
│   ├── INSTALL.md
│   ├── USER_GUIDE.md
│   ├── MIGRATION.md
│   └── TEST_RESULTS.md
├── StudentTracker.sln
└── release/
```

Release folder must contain:

- installer or self-contained executable;
- version number;
- release notes;
- sample import template;
- backup/restore instructions.

---

# 31. Final Instruction to the Implementing AI

Build the application in phases, but do not stop after planning or scaffolding.

At the end of each phase:

1. compile;
2. run tests;
3. fix errors;
4. commit working code;
5. document implementation;
6. continue to the next phase.

Use the accompanying migration workbook as the authoritative historical-data package.

Do not silently discard, merge, reinterpret or overwrite historical data.

Where a record is uncertain, preserve it and require user review.

The final result must be a practical local tool that Alex can install and use, not an enterprise platform and not a demonstration.
