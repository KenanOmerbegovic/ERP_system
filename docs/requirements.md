# Requirements

Source of truth for what the system shall do. Mirrors Chapter 2 (System Analysis) of the thesis.
When a requirement changes, update it here and in the thesis, and note the reason in `docs/open-decisions.md`.

## Conventions

- **IDs:** `FR-<module>-<nn>` for functional, `NFR-<category>-<nn>` for non-functional. IDs never change or get reused; a dropped requirement is marked `Dropped`, not deleted.
- **Priority:** `High` = needed for the core business flow or the AI evaluation · `Medium` = important, not needed for the evaluation · `Low` = desirable extension.
- **Status:** `Planned` → `In progress` → `Implemented` (with tests) · `Dropped`.
- Reference requirement IDs in branch names, commit messages, pull requests and test names (e.g. `FR_SAL_05_confirmed_invoice_cannot_be_edited`).
- Values in `[brackets]` are not decided yet. Decide them before implementing the requirement.

## Actors

| Actor | Description | Main tasks |
| --- | --- | --- |
| Administrator | Manages the company setup and users | Users, roles and permissions; company settings; master data |
| Accountant | Responsible for financial records | General ledger, payments, PDV, financial reports |
| Sales user | Handles customers and sales | Offers, orders, invoices, customer data, sales opportunities |
| Purchasing user | Handles suppliers and purchasing | Purchase orders, supplier invoices, supplier data |
| Warehouse user | Manages stock | Goods receipts and issues, LOT and serial numbers, stock counts |
| Production user | Runs production | Work orders, material consumption, quality checks |
| HR user | Manages employees | Employee records, attendance, payroll |
| Manager | Oversees the business | Dashboards, reports, questions to the AI assistant |
| AI assistant (system actor) | Answers questions on behalf of a user | Retrieves data within that user's permissions and generates answers |

> Actor names must match the roles defined in the Identity module. [Verify and align.]

## Functional requirements

### Access control and master data

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-ACC-01 | The system shall allow users to log in with an email address and password. | High | Planned |
| FR-ACC-02 | The system shall allow an administrator to create users and assign roles to them. | High | Planned |
| FR-ACC-03 | The system shall restrict access to each module according to the user's permissions. | High | Planned |
| FR-ACC-04 | The system shall keep users logged in through refresh tokens until they log out or the session expires. | Medium | Planned |
| FR-MD-01 | The system shall allow an administrator to manage company data. | High | Planned |
| FR-MD-02 | The system shall allow users to create, edit and search business partners (customers and suppliers). | High | Planned |
| FR-MD-03 | The system shall validate the format of a business partner's tax identification number (JIB). | Medium | Planned |
| FR-MD-04 | The system shall allow users to manage articles and units of measure. | High | Planned |

### Sales

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-SAL-01 | The system shall allow a sales user to create offers for customers. | High | Planned |
| FR-SAL-02 | The system shall allow converting an accepted offer into a sales order. | High | Planned |
| FR-SAL-03 | The system shall allow creating delivery notes and invoices from a sales order. | High | Planned |
| FR-SAL-04 | The system shall calculate PDV on invoice lines according to the configured rates. | High | Planned |
| FR-SAL-05 | The system shall prevent editing of confirmed documents and allow corrections only through reversal or credit notes. | High | Planned |

### Purchasing

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-PUR-01 | The system shall allow a purchasing user to create purchase orders for suppliers. | High | Implemented |
| FR-PUR-02 | The system shall allow recording supplier invoices linked to purchase orders. | High | Implemented |
| FR-PUR-03 | The system shall suggest reordering when an article's stock falls below its minimum level. | Low | Planned |

### Warehouse

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-WH-01 | The system shall record goods receipts, issues and transfers between warehouses. | High | Implemented |
| FR-WH-02 | The system shall track LOT and serial numbers for articles that require them. | Medium | Implemented |
| FR-WH-03 | The system shall prevent issuing more stock than is available. | High | Implemented |
| FR-WH-04 | The system shall show current stock levels per article and warehouse. | High | Implemented |
| FR-WH-05 | The system shall support stock counts and record the differences found. | Low | Planned |

### Finance

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-FIN-01 | The system shall maintain a chart of accounts. | High | Planned |
| FR-FIN-02 | The system shall automatically create general ledger postings when sales, purchasing and payroll documents are confirmed. | High | Planned |
| FR-FIN-03 | The system shall track open receivables and payables per business partner. | High | Planned |
| FR-FIN-04 | The system shall allow recording payments and matching them to invoices. | High | Planned |
| FR-FIN-05 | The system shall produce PDV figures for each tax period. | Medium | Planned |
| FR-FIN-06 | The system shall allow defining budgets per cost center and comparing them with actual costs. | Low | Planned |

### Production

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-PRD-01 | The system shall allow defining bills of materials for finished products. | Medium | Planned |
| FR-PRD-02 | The system shall allow creating work orders and recording their progress. | Medium | Planned |
| FR-PRD-03 | The system shall record material consumption for each work order and reduce stock accordingly. | Medium | Planned |
| FR-PRD-04 | The system shall record quality checks and link failed checks to the affected LOT. | Low | Planned |

### Human resources

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-HR-01 | The system shall allow an HR user to manage employee records and contracts. | Medium | Planned |
| FR-HR-02 | The system shall record attendance and leave. | Medium | Planned |
| FR-HR-03 | The system shall calculate monthly payroll according to the configured rules. | Medium | Planned |

### CRM and project management

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-CRM-01 | The system shall allow tracking contacts and sales opportunities by stage. | Medium | Planned |
| FR-CRM-02 | The system shall allow recording marketing campaigns and linking them to leads. | Low | Planned |
| FR-PRJ-01 | The system shall allow creating projects with tasks and milestones. | Medium | Planned |
| FR-PRJ-02 | The system shall allow recording time and costs per project. | Medium | Planned |

### Reporting

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-REP-01 | The system shall provide dashboards with key figures such as sales, stock value and margins. | Medium | Planned |
| FR-REP-02 | The system shall update dashboard figures without the user reloading the page. | Low | Planned |

### AI assistant

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-AI-01 | The system shall allow users to ask questions about business data in natural language. | High | Planned |
| FR-AI-02 | The system shall base answers only on data the asking user is permitted to access. | High | Planned |
| FR-AI-03 | The system shall retrieve relevant data and documents before generating an answer. | High | Planned |
| FR-AI-04 | The system shall allow switching between cloud-based and locally hosted models through configuration, without changes to the code. | High | Planned |
| FR-AI-05 | The system shall indicate when it cannot answer a question from the available data. | Medium | Planned |
| FR-AI-06 | The system shall log each question, the model used, the answer and the response time for the evaluation. | High | Planned |

### Data generation and evaluation

| ID | Requirement | Priority | Status |
| --- | --- | --- | --- |
| FR-GEN-01 | The system shall generate simulated business data for a configurable period through the application's own business logic. | High | Planned |
| FR-GEN-02 | The system shall reproduce the generated data exactly from a fixed seed. | High | Planned |
| FR-GEN-03 | The system shall verify the consistency of the generated data, including a balanced trial balance and consistent stock levels. | High | Planned |
| FR-EVA-01 | The system shall run a defined set of test questions against each configured model and compare the answers with the known correct answers. | High | Planned |
| FR-EVA-02 | The system shall record accuracy, response time and cost per question for each model. | High | Planned |

## Non-functional requirements

| ID | Category | Requirement | Priority | Status |
| --- | --- | --- | --- | --- |
| NFR-SEC-01 | Security | All communication between the clients and the API shall be encrypted using HTTPS. | High | Planned |
| NFR-SEC-02 | Security | Passwords shall be stored only as secure hashes, never in plain text. | High | Planned |
| NFR-SEC-03 | Security | Access tokens shall expire after [15] minutes, and refresh tokens shall be replaced each time they are used. | High | Planned |
| NFR-SEC-04 | Security | Login attempts shall be rate-limited to prevent brute-force attacks. | Medium | Planned |
| NFR-SEC-05 | Security | Secret keys shall never be stored in the source code repository. | High | Planned |
| NFR-PRI-01 | Privacy | When a locally hosted model is used, no business data shall leave the company's infrastructure. | High | Planned |
| NFR-INT-01 | Data integrity | Confirmed documents shall be immutable; corrections shall be made by reversal. | High | In progress |
| NFR-INT-02 | Data integrity | Total debits shall equal total credits in the general ledger at all times. | High | Planned |
| NFR-PER-01 | Performance | List views shall load within [2] seconds for up to [10,000] records. | Medium | Planned |
| NFR-PER-02 | Performance | The response time of the AI assistant shall be measured and reported for each model. | High | Planned |
| NFR-USA-01 | Usability | The user interface shall be available in Bosnian and English. | Medium | Planned |
| NFR-USA-02 | Usability | The user interface shall adapt to different screen sizes. | Medium | Planned |
| NFR-POR-01 | Portability | The client shall run as a web application in current browsers and as a desktop application on Windows, macOS and Linux. | High | Planned |
| NFR-MNT-01 | Maintainability | Modules shall communicate only through defined contracts and events, enforced by automated architecture tests. | High | Planned |
| NFR-MNT-02 | Maintainability | Every change shall be built and tested automatically before it is merged. | Medium | Planned |
| NFR-MNT-03 | Maintainability | Country-specific tax and payroll rules shall be isolated in a separate localization layer. | Medium | Planned |
| NFR-REL-01 | Reliability | Integration events between modules shall not be lost if a module fails temporarily. | Medium | Planned |
