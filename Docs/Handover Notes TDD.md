# Technical Design Document (TDD): Handover Notes Module

**System:** Intervention Management Platform (IMP)

**Subsystem:** Strategic Storage Module

**Feature:** Handover Notes (Grid, Add/Edit Modal, Assign-To Workflow, and Interventions Dynamic Lookup)

**Architecture:** .NET 8 Web API (Clean Architecture with Dapper) + React 18 (TypeScript, TanStack Table v8, TanStack Query v5, Tailwind CSS)

**Target Audience:** Lead Software Architects, Full-Stack Engineers, and Database Administrators

---

## 1. Executive Summary & Architectural Overview

The **Handover Notes** module provides operational shift leads, asset engineers, and managers with an auditable platform to log, track, and transfer critical operational context across operational shifts.

### Key Capabilities

* **Paginated Operational Grid:** View handover notes with filtering, server-side pagination, dynamic status badges, and formatted notes.
* **Dynamic Intervention Linking:** Links intervention IDs to site names (e.g., `19647 - BROCKHURST (DBS)(12111)`) and constructs environment-aware relative URLs (`${window.location.origin}/interventions/{id}`).
* **Multi-Select Interventions with Search:** Search and attach relevant interventions within a 72-hour operational window using popover search panels.
* **Recipient Assignment Workflow:** Modal workflow to assign or reassign handover responsibilities to eligible team members.
* **Inline Standardized Notification Banners:** Accessible success and error banners with auto-scrolling feedback mechanisms.

---

## 2. Database Layer (SQL Server)

The database design utilizes normalized relational tables under the `[IMT]` schema and encapsulated business logic via stored procedures.

```
                   +---------------------------+
                   | IMT.StratStorHandoverUser |
                   +---------------------------+
                                 | 1
                                 |
                                 | N (HandoverFromUserID / HandoverToUserID / StandbyUserID)
                                 v
  +-----------------------------------------------------------------+
  |                       IMT.StratStorHandover                     |
  +-----------------------------------------------------------------+
  | HandoverID (PK)                                                 |
  | HandoverDate                                                    |
  | HandoverFromUserID (FK)                                         |
  | HandoverToUserID (FK, Nullable)                                 |
  | StandbyUserID (FK)                                              |
  | PredictedToAchieveTarget, AllReservoirsOnTarget, etc.           |
  | ThirdHighLiftRequirementMode, ThirdHighLiftHours, etc.          |
  +-----------------------------------------------------------------+
                                 | 1
                                 |
                                 | N
                                 v
             +-----------------------------------------+
             | IMT.StratStorHandoverInterventions      |
             +-----------------------------------------+
             | HandoverID (FK)                         |
             | InterventionID (FK)                     |
             +-----------------------------------------+

```

---

### 2.1 Schema Definition

```sql
-- Main Handover Table
CREATE TABLE [IMT].[StratStorHandover]
(
    [HandoverID]                    BIGINT IDENTITY(1,1) NOT NULL,
    [HandoverDate]                  DATETIME NOT NULL,
    [HandoverFromUserID]            INT NOT NULL,
    [HandoverToUserID]              INT NULL,
    [StandbyUserID]                 INT NOT NULL,
    [PredictedToAchieveTarget]      BIT NOT NULL,
    [AllReservoirsOnTarget]         BIT NOT NULL,
    [HasNewRestrictions]            BIT NOT NULL,
    [HasPlannedInterventions]       BIT NOT NULL,
    [HasEnergyManagement]           BIT NOT NULL,
    [ThirdHighLiftRequirementMode]  NVARCHAR(10) NOT NULL, -- 'Number' or 'Text'
    [ThirdHighLiftHours]            BIGINT NULL,
    [ThirdHighLiftRequirementText]   NVARCHAR(500) NULL,
    [GSOSMytheLinkMainFlush]        NVARCHAR(50) NOT NULL, -- 'No', 'Yes', 'Yes, GSOS', 'Yes, Link Main'
    [Notes]                         NVARCHAR(MAX) NULL,
    [CreatedDate]                   DATETIME2(0) NOT NULL DEFAULT GETDATE(),
    [CreatedBy]                     INT NOT NULL,
    [ModifiedDate]                  DATETIME2(0) NULL,
    [ModifiedBy]                    INT NULL,
    CONSTRAINT [PK_StratStorHandover] PRIMARY KEY CLUSTERED ([HandoverID] ASC)
);

-- Junction Table for Linked Interventions
CREATE TABLE [IMT].[StratStorHandoverInterventions]
(
    [HandoverID]     BIGINT NOT NULL,
    [InterventionID] BIGINT NOT NULL,
    CONSTRAINT [PK_StratStorHandoverInterventions] PRIMARY KEY CLUSTERED ([HandoverID] ASC, [InterventionID] ASC),
    CONSTRAINT [FK_StratStorHandoverInterventions_Handover] FOREIGN KEY ([HandoverID]) 
        REFERENCES [IMT].[StratStorHandover] ([HandoverID]) ON DELETE CASCADE
);
GO

```

---

### 2.2 Stored Procedures

#### 1. `[IMT].[SPStratStorGetHandovers]`

Fetches paginated handover records with user full names and initials.

```sql
CREATE PROCEDURE [IMT].[SPStratStorGetHandovers]
(
    @PageNumber INT = 1,
    @PageSize INT = 10,
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL,
    @SearchText NVARCHAR(200) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        h.HandoverID,
        h.HandoverDate,
        CONVERT(VARCHAR(10), h.HandoverDate, 103) AS HandoverDateDisplay,
        h.HandoverFromUserID,
        uFrom.FullName AS HandoverFromName,
        uFrom.ShortCode AS HandoverFromInitials,
        h.HandoverToUserID,
        uTo.FullName AS HandoverToName,
        uTo.ShortCode AS HandoverToInitials,
        h.StandbyUserID,
        uStandby.FullName AS StandbyName,
        h.PredictedToAchieveTarget,
        h.AllReservoirsOnTarget,
        h.HasNewRestrictions,
        h.HasPlannedInterventions,
        h.HasEnergyManagement,
        h.ThirdHighLiftRequirementMode,
        h.ThirdHighLiftHours,
        h.ThirdHighLiftRequirementText,
        h.GSOSMytheLinkMainFlush,
        h.Notes,
        h.CreatedDate,
        COUNT(1) OVER() AS TotalRecords
    FROM [IMT].[StratStorHandover] h
    INNER JOIN [IMT].[StratStorHandoverUser] uFrom ON h.HandoverFromUserID = uFrom.HandoverUserID
    LEFT JOIN [IMT].[StratStorHandoverUser] uTo ON h.HandoverToUserID = uTo.HandoverUserID
    INNER JOIN [IMT].[StratStorHandoverUser] uStandby ON h.StandbyUserID = uStandby.HandoverUserID
    WHERE (@FromDate IS NULL OR h.HandoverDate >= @FromDate)
      AND (@ToDate IS NULL OR h.HandoverDate <= @ToDate)
      AND (@SearchText IS NULL OR h.Notes LIKE '%' + @SearchText + '%' 
           OR uFrom.FullName LIKE '%' + @SearchText + '%'
           OR uStandby.FullName LIKE '%' + @SearchText + '%')
    ORDER BY h.HandoverDate DESC, h.HandoverID DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
GO

```

#### 2. `[IMT].[SPStratStorInsertHandover]`

Creates a handover entry and returns the generated primary key.

```sql
CREATE PROCEDURE [IMT].[SPStratStorInsertHandover]
(
    @HandoverDate DATETIME = NULL,
    @HandoverFromUserID INT,
    @HandoverToUserID INT = NULL,
    @StandbyUserID INT,
    @PredictedToAchieveTarget BIT,
    @AllReservoirsOnTarget BIT,
    @HasNewRestrictions BIT,
    @HasPlannedInterventions BIT,
    @HasEnergyManagement BIT,
    @ThirdHighLiftRequirementMode NVARCHAR(10),
    @ThirdHighLiftHours BIGINT = NULL,
    @ThirdHighLiftRequirementText NVARCHAR(200) = NULL,
    @GSOSMytheLinkMainFlush NVARCHAR(50),
    @Notes NVARCHAR(MAX),
    @CreatedBy INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @HandoverDate IS NULL
        SET @HandoverDate = CAST(GETDATE() AS DATE);

    INSERT INTO [IMT].[StratStorHandover]
    (
        HandoverDate, HandoverFromUserID, HandoverToUserID, StandbyUserID,
        PredictedToAchieveTarget, AllReservoirsOnTarget, HasNewRestrictions,
        HasPlannedInterventions, HasEnergyManagement, ThirdHighLiftRequirementMode,
        ThirdHighLiftHours, ThirdHighLiftRequirementText, GSOSMytheLinkMainFlush,
        Notes, CreatedDate, CreatedBy
    )
    VALUES
    (
        @HandoverDate, @HandoverFromUserID, @HandoverToUserID, @StandbyUserID,
        @PredictedToAchieveTarget, @AllReservoirsOnTarget, @HasNewRestrictions,
        @HasPlannedInterventions, @HasEnergyManagement, @ThirdHighLiftRequirementMode,
        @ThirdHighLiftHours, @ThirdHighLiftRequirementText, @GSOSMytheLinkMainFlush,
        @Notes, GETDATE(), ISNULL(@CreatedBy, @HandoverFromUserID)
    );

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS NewHandoverID;
END;
GO

```

#### 3. `[IMT].[SPStratStorUpdateHandover]`

Updates an existing handover record.

```sql
CREATE PROCEDURE [IMT].[SPStratStorUpdateHandover]
(
    @HandoverID BIGINT,
    @HandoverDate DATETIME = NULL,
    @StandbyUserID INT,
    @PredictedToAchieveTarget BIT,
    @AllReservoirsOnTarget BIT,
    @HasNewRestrictions BIT,
    @HasPlannedInterventions BIT,
    @HasEnergyManagement BIT,
    @ThirdHighLiftRequirementMode NVARCHAR(10),
    @ThirdHighLiftHours BIGINT = NULL,
    @ThirdHighLiftRequirementText NVARCHAR(200) = NULL,
    @GSOSMytheLinkMainFlush NVARCHAR(50),
    @Notes NVARCHAR(MAX),
    @ModifiedBy INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [IMT].[StratStorHandover]
    SET 
        HandoverDate = ISNULL(@HandoverDate, HandoverDate),
        StandbyUserID = @StandbyUserID,
        PredictedToAchieveTarget = @PredictedToAchieveTarget,
        AllReservoirsOnTarget = @AllReservoirsOnTarget,
        HasNewRestrictions = @HasNewRestrictions,
        HasPlannedInterventions = @HasPlannedInterventions,
        HasEnergyManagement = @HasEnergyManagement,
        ThirdHighLiftRequirementMode = @ThirdHighLiftRequirementMode,
        ThirdHighLiftHours = @ThirdHighLiftHours,
        ThirdHighLiftRequirementText = @ThirdHighLiftRequirementText,
        GSOSMytheLinkMainFlush = @GSOSMytheLinkMainFlush,
        Notes = @Notes,
        ModifiedDate = GETDATE(),
        ModifiedBy = @ModifiedBy
    WHERE HandoverID = @HandoverID;
END;
GO

```

#### 4. `[IMT].[SPStratStorAssignHandoverTo]`

Updates the assigned recipient (`HandoverToUserID`) for a specific record.

```sql
CREATE PROCEDURE [IMT].[SPStratStorAssignHandoverTo]
(
    @HandoverID BIGINT,
    @HandoverToUserID INT,
    @ModifiedBy INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [IMT].[StratStorHandover]
    SET 
        HandoverToUserID = @HandoverToUserID,
        ModifiedDate = GETDATE(),
        ModifiedBy = @ModifiedBy
    WHERE HandoverID = @HandoverID;
END;
GO

```

#### 5. `[IMT].[SPStratStorGetInterventionsLookup]`

Retrieves intervention IDs and site names for form dropdowns and table link resolution.

```sql
CREATE PROCEDURE [IMT].[SPStratStorGetInterventionsLookup]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        ID AS Id, 
        CAST(ID AS NVARCHAR(50)) + ' - ' + SiteName AS Name
    FROM [IMT].[Interventions]
    WHERE SiteName IS NOT NULL AND LTRIM(RTRIM(SiteName)) <> ''
    ORDER BY ID DESC;
END;
GO

```

---

## 3. Backend API Layer (.NET 8 Clean Architecture)

The backend follows Clean Architecture principles: **Domain/DTOs** -> **Application Services** -> **Infrastructure Repositories** -> **API Controllers**.

---

### 3.1 Data Transfer Objects (DTOs)

```csharp
namespace InterventionManagement.Application.DTO.StrategicStorage
{
    public class StrategicStorageHandoverDTO
    {
        public long HandoverId { get; set; }
        public DateTime HandoverDate { get; set; }
        public string? HandoverDateDisplay { get; set; }
        public int HandoverFromUserId { get; set; }
        public string? HandoverFromName { get; set; }
        public string? HandoverFromInitials { get; set; }
        public int? HandoverToUserId { get; set; }
        public string? HandoverToName { get; set; }
        public string? HandoverToInitials { get; set; }
        public int StandbyUserId { get; set; }
        public string? StandbyName { get; set; }
        public bool PredictedToAchieveTarget { get; set; }
        public bool AllReservoirsOnTarget { get; set; }
        public bool HasNewRestrictions { get; set; }
        public bool HasPlannedInterventions { get; set; }
        public bool HasEnergyManagement { get; set; }
        public string ThirdHighLiftRequirementMode { get; set; } = "Number";
        public long? ThirdHighLiftHours { get; set; }
        public string? ThirdHighLiftRequirementText { get; set; }
        public string GSOSMytheLinkMainFlush { get; set; } = "No";
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public List<long> InterventionIds { get; set; } = new();
        public int TotalRecords { get; set; }
    }

    public class StrategicStorageHandoverSaveDTO
    {
        public long? HandoverId { get; set; }
        public DateTime? HandoverDate { get; set; }
        public int HandoverFromUserId { get; set; }
        public int? HandoverToUserId { get; set; }
        public int StandbyUserId { get; set; }
        public bool PredictedToAchieveTarget { get; set; }
        public bool AllReservoirsOnTarget { get; set; }
        public bool HasNewRestrictions { get; set; }
        public bool HasPlannedInterventions { get; set; }
        public bool HasEnergyManagement { get; set; }
        public string ThirdHighLiftRequirementMode { get; set; } = "Number";
        public long? ThirdHighLiftHours { get; set; }
        public string? ThirdHighLiftRequirementText { get; set; }
        public string GSOSMytheLinkMainFlush { get; set; } = "No";
        public string Notes { get; set; } = string.Empty;
        public List<long> InterventionIds { get; set; } = new();
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
    }

    public class AssignHandoverToDTO
    {
        public long HandoverId { get; set; }
        public int HandoverToUserId { get; set; }
        public int? ModifiedBy { get; set; }
    }

    public class HandoverQueryDTO
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SearchText { get; set; }
    }
}

```

---

### 3.2 Repository Layer (Dapper Implementation)

#### Interface (`IStrategicStorageHandoverRepository.cs`)

```csharp
namespace InterventionManagement.Infrastructure.Repositories.StrategicStorage
{
    public interface IStrategicStorageHandoverRepository
    {
        Task<PagedResult<StrategicStorageHandoverDTO>> GetHandoversAsync(HandoverQueryDTO query);
        Task<StrategicStorageHandoverDTO?> GetHandoverByIdAsync(long id);
        Task<long> InsertHandoverAsync(StrategicStorageHandoverSaveDTO dto);
        Task<bool> UpdateHandoverAsync(StrategicStorageHandoverSaveDTO dto);
        Task<bool> AssignHandoverToAsync(AssignHandoverToDTO dto);
        Task<IEnumerable<DropdownOptionDto>> GetHandoverUsersAsync();
        Task<IEnumerable<DropdownOptionDto>> GetInterventionsLookupAsync();
    }
}

```

#### Implementation (`StrategicStorageHandoverRepository.cs`)

```csharp
namespace InterventionManagement.Infrastructure.Repositories.StrategicStorage
{
    public class StrategicStorageHandoverRepository : GenericRepository, IStrategicStorageHandoverRepository
    {
        public StrategicStorageHandoverRepository(IConfiguration configuration) : base(configuration) { }

        public async Task<PagedResult<StrategicStorageHandoverDTO>> GetHandoversAsync(HandoverQueryDTO query)
        {
            const string procName = "[IMT].[SPStratStorGetHandovers]";
            var parameters = new DynamicParameters();
            parameters.Add("@PageNumber", query.PageNumber);
            parameters.Add("@PageSize", query.PageSize);
            parameters.Add("@FromDate", query.FromDate);
            parameters.Add("@ToDate", query.ToDate);
            parameters.Add("@SearchText", query.SearchText);

            var list = (await base.GetAllListAsyncByProcedure<StrategicStorageHandoverDTO>(procName, parameters)).ToList();
            int total = list.FirstOrDefault()?.TotalRecords ?? 0;

            // Fetch linked intervention mappings for each record in page
            if (list.Any())
            {
                var ids = list.Select(x => x.HandoverId).ToList();
                var mapping = await GetLinkedInterventionsForHandovers(ids);
                foreach (var item in list)
                {
                    if (mapping.TryGetValue(item.HandoverId, out var linkedIds))
                        item.InterventionIds = linkedIds;
                }
            }

            return new PagedResult<StrategicStorageHandoverDTO>(list, total, query.PageNumber, query.PageSize);
        }

        public async Task<long> InsertHandoverAsync(StrategicStorageHandoverSaveDTO dto)
        {
            const string procName = "[IMT].[SPStratStorInsertHandover]";
            var parameters = new DynamicParameters();
            parameters.Add("@HandoverDate", dto.HandoverDate ?? DateTime.Now.Date);
            parameters.Add("@HandoverFromUserID", dto.HandoverFromUserId);
            parameters.Add("@HandoverToUserID", dto.HandoverToUserId);
            parameters.Add("@StandbyUserID", dto.StandbyUserId);
            parameters.Add("@PredictedToAchieveTarget", dto.PredictedToAchieveTarget);
            parameters.Add("@AllReservoirsOnTarget", dto.AllReservoirsOnTarget);
            parameters.Add("@HasNewRestrictions", dto.HasNewRestrictions);
            parameters.Add("@HasPlannedInterventions", dto.HasPlannedInterventions);
            parameters.Add("@HasEnergyManagement", dto.HasEnergyManagement);
            parameters.Add("@ThirdHighLiftRequirementMode", dto.ThirdHighLiftRequirementMode);
            parameters.Add("@ThirdHighLiftHours", dto.ThirdHighLiftHours);
            parameters.Add("@ThirdHighLiftRequirementText", dto.ThirdHighLiftRequirementText);
            parameters.Add("@GSOSMytheLinkMainFlush", dto.GSOSMytheLinkMainFlush);
            parameters.Add("@Notes", dto.Notes);
            parameters.Add("@CreatedBy", dto.CreatedBy ?? dto.HandoverFromUserId);

            var handoverId = await base.ExecuteScalarStoredProcedureAsync<long>(procName, parameters);

            if (handoverId > 0 && dto.InterventionIds != null && dto.InterventionIds.Any())
            {
                await SaveHandoverInterventions(handoverId, dto.InterventionIds);
            }

            return handoverId;
        }

        public async Task<bool> UpdateHandoverAsync(StrategicStorageHandoverSaveDTO dto)
        {
            if (!dto.HandoverId.HasValue) return false;

            const string procName = "[IMT].[SPStratStorUpdateHandover]";
            var parameters = new DynamicParameters();
            parameters.Add("@HandoverID", dto.HandoverId.Value);
            parameters.Add("@HandoverDate", dto.HandoverDate);
            parameters.Add("@StandbyUserID", dto.StandbyUserId);
            parameters.Add("@PredictedToAchieveTarget", dto.PredictedToAchieveTarget);
            parameters.Add("@AllReservoirsOnTarget", dto.AllReservoirsOnTarget);
            parameters.Add("@HasNewRestrictions", dto.HasNewRestrictions);
            parameters.Add("@HasPlannedInterventions", dto.HasPlannedInterventions);
            parameters.Add("@HasEnergyManagement", dto.HasEnergyManagement);
            parameters.Add("@ThirdHighLiftRequirementMode", dto.ThirdHighLiftRequirementMode);
            parameters.Add("@ThirdHighLiftHours", dto.ThirdHighLiftHours);
            parameters.Add("@ThirdHighLiftRequirementText", dto.ThirdHighLiftRequirementText);
            parameters.Add("@GSOSMytheLinkMainFlush", dto.GSOSMytheLinkMainFlush);
            parameters.Add("@Notes", dto.Notes);
            parameters.Add("@ModifiedBy", dto.ModifiedBy ?? dto.HandoverFromUserId);

            await base.ExecuteStoredProcedureAsync(procName, parameters);
            await SaveHandoverInterventions(dto.HandoverId.Value, dto.InterventionIds);

            return true;
        }

        public async Task<bool> AssignHandoverToAsync(AssignHandoverToDTO dto)
        {
            const string procName = "[IMT].[SPStratStorAssignHandoverTo]";
            var parameters = new DynamicParameters();
            parameters.Add("@HandoverID", dto.HandoverId);
            parameters.Add("@HandoverToUserID", dto.HandoverToUserId);
            parameters.Add("@ModifiedBy", dto.ModifiedBy);

            await base.ExecuteStoredProcedureAsync(procName, parameters);
            return true;
        }

        public async Task<IEnumerable<DropdownOptionDto>> GetInterventionsLookupAsync()
        {
            const string procName = "[IMT].[SPStratStorGetInterventionsLookup]";
            return await base.GetAllListAsyncByProcedure<DropdownOptionDto>(procName, null);
        }

        private async Task SaveHandoverInterventions(long handoverId, List<long> interventionIds)
        {
            const string deleteSql = "DELETE FROM [IMT].[StratStorHandoverInterventions] WHERE HandoverID = @HandoverID";
            await base.ExecuteSqlAsync(deleteSql, new { HandoverID = handoverId });

            if (interventionIds != null && interventionIds.Any())
            {
                const string insertSql = "INSERT INTO [IMT].[StratStorHandoverInterventions] (HandoverID, InterventionID) VALUES (@HandoverID, @InterventionID)";
                var rows = interventionIds.Select(id => new { HandoverID = handoverId, InterventionID = id });
                await base.ExecuteBatchSqlAsync(insertSql, rows);
            }
        }
    }
}

```

---

### 3.3 Controller Layer

```csharp
namespace InterventionManagement.API.Controllers.StrategicStorage
{
    [ApiController]
    [Route("api/strategic-storage")]
    public class StrategicStorageHandoverController : ControllerBase
    {
        private readonly IStrategicStorageHandoverService _service;

        public StrategicStorageHandoverController(IStrategicStorageHandoverService service)
        {
            _service = service;
        }

        [HttpGet("handovers")]
        public async Task<IActionResult> GetHandovers([FromQuery] HandoverQueryDTO query)
        {
            var result = await _service.GetHandoversAsync(query);
            return Ok(result);
        }

        [HttpPost("handovers")]
        public async Task<IActionResult> CreateHandover([FromBody] StrategicStorageHandoverSaveDTO dto)
        {
            var id = await _service.InsertHandoverAsync(dto);
            return Ok(new { id, message = "Handover created successfully." });
        }

        [HttpPut("handovers/{id:long}")]
        public async Task<IActionResult> UpdateHandover(long id, [FromBody] StrategicStorageHandoverSaveDTO dto)
        {
            dto.HandoverId = id;
            var success = await _service.UpdateHandoverAsync(dto);
            if (!success) return NotFound();
            return Ok(new { message = "Handover updated successfully." });
        }

        [HttpPut("handovers/{id:long}/assign-to")]
        public async Task<IActionResult> AssignHandoverTo(long id, [FromBody] AssignHandoverToDTO dto)
        {
            dto.HandoverId = id;
            var success = await _service.AssignHandoverToAsync(dto);
            if (!success) return NotFound();
            return Ok(new { message = "Handover recipient assigned successfully." });
        }

        [HttpGet("interventions-lookup")]
        public async Task<IActionResult> GetInterventionsLookup()
        {
            var list = await _service.GetInterventionsLookupAsync();
            return Ok(list);
        }
    }
}

```

---

## 4. Frontend Architecture Layer (React + TypeScript)

The frontend uses modular component design. Shared contracts sit in `@/lib/types/strategicStorage`, and API integration methods are abstracted in `@/lib/api/strategicStorage`.

---

### 4.1 TypeScript Data Contracts (`src/lib/types/strategicStorage/handover.ts`)

```typescript
import { BaseQueryDto } from './common';

export interface HandoverDto {
  handoverId: number;
  handoverDate: string;
  handoverDateDisplay?: string;
  handoverFromUserId: number;
  handoverFromName?: string;
  handoverFromInitials?: string;
  handoverToUserId?: number;
  handoverToName?: string;
  handoverToInitials?: string;
  standbyUserId: number;
  standbyName?: string;
  predictedToAchieveTarget: boolean;
  allReservoirsOnTarget: boolean;
  hasNewRestrictions: boolean;
  hasPlannedInterventions: boolean;
  hasEnergyManagement: boolean;
  thirdHighLiftRequirementMode: 'Number' | 'Text';
  thirdHighLiftHours?: number;
  thirdHighLiftRequirementText?: string;
  gsosMytheLinkMainFlush: 'No' | 'Yes' | 'Yes, GSOS' | 'Yes, Link Main';
  notes: string;
  createdDate: string;
  interventionIds: number[];
}

export interface SaveHandoverDto {
  handoverId?: number;
  handoverDate?: string;
  handoverFromUserId: number;
  handoverToUserId?: number;
  standbyUserId: number;
  predictedToAchieveTarget: boolean;
  allReservoirsOnTarget: boolean;
  hasNewRestrictions: boolean;
  hasPlannedInterventions: boolean;
  hasEnergyManagement: boolean;
  thirdHighLiftRequirementMode: 'Number' | 'Text';
  thirdHighLiftHours?: number;
  thirdHighLiftRequirementText?: string;
  gsosMytheLinkMainFlush: 'No' | 'Yes' | 'Yes, GSOS' | 'Yes, Link Main';
  notes: string;
  interventionIds: number[];
  createdBy?: number;
  modifiedBy?: number;
}

export interface AssignHandoverToDto {
  handoverId: number;
  handoverToUserId: number;
  modifiedBy?: number;
}

export type HandoverQueryDto = BaseQueryDto;

```

---

### 4.2 Frontend API Layer (`src/lib/api/strategicStorage/handoverApi.ts`)

```typescript
import { apiclient } from '@/lib/api/client';
import {
  HandoverDto,
  SaveHandoverDto,
  HandoverQueryDto,
  DropdownOptionDto,
  PagedResponse,
} from '@/lib/types/strategicStorage';

const BASE_ENDPOINT = '/api/strategic-storage';

export async function getHandovers(params: HandoverQueryDto): Promise<PagedResponse<HandoverDto>> {
  const response = await apiclient.get<PagedResponse<HandoverDto>>(`${BASE_ENDPOINT}/handovers`, {
    params: {
      PageNumber: params.pageNumber,
      PageSize: params.pageSize,
      FromDate: params.fromDate || undefined,
      ToDate: params.toDate || undefined,
      SearchText: params.searchText?.trim() || undefined,
    },
  });
  return response.data;
}

export async function createHandover(payload: SaveHandoverDto): Promise<{ id: number }> {
  const response = await apiclient.post<{ id: number }>(`${BASE_ENDPOINT}/handovers`, payload);
  return response.data;
}

export async function updateHandover(id: number, payload: SaveHandoverDto): Promise<void> {
  await apiclient.put(`${BASE_ENDPOINT}/handovers/${id}`, payload);
}

export async function assignHandoverTo(id: number, handoverToUserId: number, modifiedBy?: number): Promise<void> {
  await apiclient.put(`${BASE_ENDPOINT}/handovers/${id}/assign-to`, {
    handoverId: id,
    handoverToUserId,
    modifiedBy,
  });
}

export async function getHandoverUsers(): Promise<DropdownOptionDto[]> {
  const response = await apiclient.get<DropdownOptionDto[]>(`${BASE_ENDPOINT}/handover-users`);
  return response.data || [];
}

export async function getInterventionsLookup(): Promise<DropdownOptionDto[]> {
  const response = await apiclient.get<DropdownOptionDto[]>(`${BASE_ENDPOINT}/interventions-lookup`);
  return response.data || [];
}

```

---

### 4.3 Custom React Query Hook (`src/features/SS/handover/hooks/useHandovers.ts`)

Encapsulates data fetching, pagination state, and mutation logic using TanStack Query v5.

```typescript
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { getHandovers, createHandover, updateHandover } from '@/lib/api/strategicStorage';
import { HandoverQueryDto, SaveHandoverDto } from '@/lib/types/strategicStorage';

export function useHandovers() {
  const queryClient = useQueryClient();

  const [filters, setFilters] = useState<HandoverQueryDto>({
    pageNumber: 1,
    pageSize: 10,
    fromDate: '',
    toDate: '',
    searchText: '',
  });

  const query = useQuery({
    queryKey: ['strategicStorageHandovers', filters],
    queryFn: () => getHandovers(filters),
    placeholderData: keepPreviousData,
    staleTime: 10000,
  });

  const createMutation = useMutation({
    mutationFn: (newHandover: SaveHandoverDto) => createHandover(newHandover),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['strategicStorageHandovers'] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: SaveHandoverDto }) =>
      updateHandover(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['strategicStorageHandovers'] });
    },
  });

  const setPage = (pageNumber: number) => {
    setFilters((prev) => ({ ...prev, pageNumber }));
  };

  const setPageSize = (pageSize: number) => {
    setFilters((prev) => ({ ...prev, pageSize, pageNumber: 1 }));
  };

  return {
    data: query.data?.items ?? [],
    totalRecords: query.data?.totalRecords ?? 0,
    totalPages: query.data?.totalPages ?? 1,
    isLoading: query.isLoading,
    filters,
    setPage,
    setPageSize,
    refetch: query.refetch,
    createHandoverNote: createMutation.mutateAsync,
    isCreating: createMutation.isPending,
    updateHandoverNote: updateMutation.mutateAsync,
    isUpdating: updateMutation.isPending,
  };
}

```

---

### 4.4 Column Definitions (`src/features/SS/handover/components/handoverColumns.tsx`)

Configures TanStack Table columns, including dynamic status badge colors, multi-line notes width styling, and link generation for planned interventions.

```tsx
import React from 'react';
import { ColumnDef } from '@tanstack/react-table';
import { HandoverDto } from '@/lib/types/strategicStorage';

interface ColumnProps {
  onEdit: (handover: HandoverDto) => void;
  onAssignHandoverTo: (handover: HandoverDto) => void;
  interventionsMap?: Record<number, string>;
}

export const getHandoverColumns = ({
  onEdit,
  onAssignHandoverTo,
  interventionsMap = {},
}: ColumnProps): ColumnDef<HandoverDto>[] => [
  {
    accessorKey: 'handoverDateDisplay',
    header: 'Date',
    cell: (info) => (
      <span className="whitespace-nowrap font-medium text-gray-800">
        {(info.getValue() as string) || info.row.original.handoverDate || '-'}
      </span>
    ),
  },

  // Handover From
  {
    accessorKey: 'handoverFromInitials',
    header: 'Handover From',
    cell: (info) => (
      <span
        className="inline-flex items-center justify-center min-w-[28px] h-7 px-2 bg-gray-100 border border-gray-200 rounded-full text-[10px] font-bold text-gray-700 uppercase whitespace-nowrap shadow-2xs"
        title={info.row.original.handoverFromName || 'Handover From'}
      >
        {(info.getValue() as string) || 'NA'}
      </span>
    ),
  },

  // Handover To (Positioned between Handover From and Standby)
  {
    accessorKey: 'handoverToInitials',
    header: 'Handover To',
    cell: (info) => {
      const row = info.row.original;
      if (row.handoverToUserId && (row.handoverToName || row.handoverToInitials)) {
        return (
          <div className="flex items-center gap-1.5">
            <span
              className="inline-flex items-center justify-center min-w-[28px] h-7 px-2 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-full text-[10px] font-bold uppercase whitespace-nowrap"
              title={row.handoverToName || 'Assigned Recipient'}
            >
              {row.handoverToInitials || row.handoverToName}
            </span>
            <button
              type="button"
              onClick={() => onAssignHandoverTo(row)}
              className="text-[10px] text-blue-600 hover:underline font-semibold"
              title="Change Recipient"
            >
              Edit
            </button>
          </div>
        );
      }

      return (
        <button
          type="button"
          onClick={() => onAssignHandoverTo(row)}
          className="px-2.5 py-1 bg-white border border-gray-300 rounded text-xs font-medium text-gray-700 hover:bg-gray-50 shadow-2xs transition-colors flex items-center gap-1"
        >
          <span>⊕</span> Assign
        </button>
      );
    },
  },

  // Standby (Italic, non-bold per specification)
  {
    accessorKey: 'standbyName',
    header: 'Standby',
    cell: (info) => (
      <span className="font-normal italic text-gray-800 whitespace-nowrap">
        {(info.getValue() as string) || '-'}
      </span>
    ),
  },

  {
    accessorKey: 'predictedToAchieveTarget',
    header: 'Predicted to achieve 82% target tomorrow at 06:00?',
    cell: (info) => {
      const val = info.getValue() as boolean;
      return (
        <span
          className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold whitespace-nowrap ${
            val ? 'bg-emerald-100 text-emerald-800 border border-emerald-200' : 'bg-red-100 text-red-800 border border-red-200'
          }`}
        >
          {val ? 'Yes' : 'No'}
        </span>
      );
    },
  },
  {
    accessorKey: 'allReservoirsOnTarget',
    header: 'All Reservoirs on Target?',
    cell: (info) => {
      const val = info.getValue() as boolean;
      return (
        <span
          className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold whitespace-nowrap ${
            val ? 'bg-emerald-100 text-emerald-800 border border-emerald-200' : 'bg-red-100 text-red-800 border border-red-200'
          }`}
        >
          {val ? 'Yes' : 'No'}
        </span>
      );
    },
  },
  {
    accessorKey: 'hasNewRestrictions',
    header: 'New Restrictions',
    cell: (info) => {
      const val = info.getValue() as boolean;
      return (
        <span
          className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold whitespace-nowrap ${
            val ? 'bg-emerald-100 text-emerald-800 border border-emerald-200' : 'bg-red-100 text-red-800 border border-red-200'
          }`}
        >
          {val ? 'Yes' : 'No'}
        </span>
      );
    },
  },
  {
    accessorKey: 'hasPlannedInterventions',
    header: 'Interventions Planned for Tomorrow or Weekend',
    cell: (info) => {
      const val = info.getValue() as boolean;
      return (
        <span
          className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold whitespace-nowrap ${
            val ? 'bg-emerald-100 text-emerald-800 border border-emerald-200' : 'bg-red-100 text-red-800 border border-red-200'
          }`}
        >
          {val ? 'Yes' : 'No'}
        </span>
      );
    },
  },
  {
    accessorKey: 'hasEnergyManagement',
    header: 'Energy Management',
    cell: (info) => {
      const val = info.getValue() as boolean;
      return (
        <span
          className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold whitespace-nowrap ${
            val ? 'bg-emerald-100 text-emerald-800 border border-emerald-200' : 'bg-red-100 text-red-800 border border-red-200'
          }`}
        >
          {val ? 'Yes' : 'No'}
        </span>
      );
    },
  },
  {
    id: 'thirdHighLift',
    header: 'No. Hours 3rd Highlift Req.',
    cell: (info) => {
      const row = info.row.original;
      return (
        <span className="font-semibold text-gray-800">
          {row.thirdHighLiftRequirementMode === 'Number'
            ? row.thirdHighLiftHours ?? '-'
            : row.thirdHighLiftRequirementText || '-'}
        </span>
      );
    },
  },
  {
    accessorKey: 'gsosMytheLinkMainFlush',
    header: 'GSOS/Mythe Link Main Flush?',
    cell: (info) => <span className="font-semibold text-gray-800">{info.getValue() as string}</span>,
  },
  // Notes Column - Enforces minimum width to avoid vertical crowding
  {
    accessorKey: 'notes',
    header: 'Notes',
    cell: (info) => (
      <div className="min-w-[380px] max-w-xl text-xs text-gray-700 whitespace-pre-wrap py-1 leading-relaxed">
        {(info.getValue() as string) || '-'}
      </div>
    ),
  },
  // Dynamic Interventions Link Column
  {
    id: 'interventions',
    header: 'Planned Interventions (Links)',
    cell: (info) => {
      const ids = info.row.original.interventionIds || [];
      if (!ids.length) return <span className="text-gray-400">-</span>;

      const baseUrl = window.location.origin;

      return (
        <div className="flex flex-col gap-1 min-w-[200px]">
          {ids.map((id) => {
            const label = interventionsMap[id] || `${id} - Intervention`;
            return (
              <a
                key={id}
                href={`${baseUrl}/interventions/${id}`}
                target="_blank"
                rel="noreferrer"
                className="text-xs text-blue-600 hover:underline font-medium italic flex items-center gap-1"
              >
                {label}
              </a>
            );
          })}
        </div>
      );
    },
  },
  {
    id: 'actions',
    header: 'Actions',
    cell: (info) => (
      <button
        type="button"
        onClick={() => onEdit(info.row.original)}
        className="p-1.5 text-gray-500 hover:text-slate-900 rounded hover:bg-gray-100 transition-colors"
        title="Edit Handover Note"
      >
        ✏️
      </button>
    ),
  },
];

```

---

### 4.5 Data Table Component (`src/features/SS/handover/components/HandoverTable.tsx`)

```tsx
import React, { useMemo, useState } from 'react';
import {
  useReactTable,
  getCoreRowModel,
  flexRender,
  SortingState,
  getSortedRowModel,
} from '@tanstack/react-table';
import { useQuery } from '@tanstack/react-query';
import { HandoverDto } from '@/lib/types/strategicStorage';
import { getInterventionsLookup } from '@/lib/api/strategicStorage';
import { getHandoverColumns } from './handoverColumns';

interface HandoverTableProps {
  data: HandoverDto[];
  isLoading: boolean;
  onEdit: (handover: HandoverDto) => void;
  onAssignHandoverTo: (handover: HandoverDto) => void;
}

export const HandoverTable: React.FC<HandoverTableProps> = ({
  data,
  isLoading,
  onEdit,
  onAssignHandoverTo,
}) => {
  const [sorting, setSorting] = useState<SortingState>([]);

  // Query intervention lookup dictionary for dynamic link rendering
  const { data: lookupList } = useQuery({
    queryKey: ['interventionsLookup'],
    queryFn: getInterventionsLookup,
    staleTime: 5 * 60 * 1000,
  });

  const interventionsMap = useMemo(() => {
    const map: Record<number, string> = {};
    if (lookupList && Array.isArray(lookupList)) {
      lookupList.forEach((item) => {
        if (item.id && item.name) {
          map[Number(item.id)] = item.name;
        }
      });
    }
    return map;
  }, [lookupList]);

  const columns = useMemo(
    () => getHandoverColumns({ onEdit, onAssignHandoverTo, interventionsMap }),
    [onEdit, onAssignHandoverTo, interventionsMap]
  );

  const table = useReactTable({
    data,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  });

  return (
    <div className="overflow-x-auto min-h-[400px] relative border border-gray-200 rounded-md bg-white">
      {isLoading && (
        <div className="absolute inset-0 bg-white/70 flex items-center justify-center z-10 backdrop-blur-xs">
          <span className="text-xs font-semibold text-gray-600">Loading handover notes...</span>
        </div>
      )}

      <table className="w-full text-left border-collapse text-xs">
        <thead>
          {table.getHeaderGroups().map((headerGroup) => (
            <tr
              key={headerGroup.id}
              className="border-b border-gray-200 uppercase text-gray-500 bg-gray-50/80 font-semibold"
            >
              {headerGroup.headers.map((header) => (
                <th key={header.id} className="p-3">
                  {header.isPlaceholder
                    ? null
                    : flexRender(header.column.columnDef.header, header.getContext())}
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody className="divide-y divide-gray-100 text-gray-700">
          {table.getRowModel().rows.length > 0 ? (
            table.getRowModel().rows.map((row) => (
              <tr key={row.id} className="hover:bg-gray-50/60 transition-colors">
                {row.getVisibleCells().map((cell) => (
                  <td key={cell.id} className="p-3">
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            ))
          ) : (
            <tr>
              <td colSpan={columns.length} className="p-8 text-center text-gray-400 font-medium">
                No handover notes found.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
};

```

---

### 4.6 Pagination Component (`src/features/SS/handover/components/HandoverPagination.tsx`)

Renders controls matching the platform's pagination design (`First Prev 1 2 3 ... 450 Next Last`).

```tsx
import React from 'react';

interface HandoverPaginationProps {
  pageNumber: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export const HandoverPagination: React.FC<HandoverPaginationProps> = ({
  pageNumber,
  pageSize,
  totalRecords,
  totalPages,
  onPageChange,
}) => {
  const startRecord = totalRecords > 0 ? (pageNumber - 1) * pageSize + 1 : 0;
  const endRecord = Math.min(pageNumber * pageSize, totalRecords);

  const getPageNumbers = () => {
    const pages: (number | string)[] = [];
    if (totalPages <= 7) {
      for (let i = 1; i <= totalPages; i++) pages.push(i);
    } else {
      pages.push(1);
      if (pageNumber > 3) pages.push('...');
      const start = Math.max(2, pageNumber - 1);
      const end = Math.min(totalPages - 1, pageNumber + 1);
      for (let i = start; i <= end; i++) {
        if (i > 1 && i < totalPages) pages.push(i);
      }
      if (pageNumber < totalPages - 2) pages.push('...');
      pages.push(totalPages);
    }
    return pages;
  };

  return (
    <div className="flex flex-col sm:flex-row items-center justify-between gap-4 pt-3 mt-2 border-t border-gray-100 text-xs">
      <div className="text-gray-500">
        Showing <span className="font-semibold text-gray-800">{startRecord}</span> to{' '}
        <span className="font-semibold text-gray-800">{endRecord}</span> of{' '}
        <span className="font-semibold text-gray-800">{totalRecords.toLocaleString()}</span> records
      </div>

      <div className="flex items-center gap-1.5">
        <button
          type="button"
          disabled={pageNumber <= 1}
          onClick={() => onPageChange(1)}
          className="px-2.5 py-1 border border-gray-300 rounded bg-white text-gray-700 font-medium disabled:opacity-40 disabled:cursor-not-allowed hover:bg-gray-50 transition-colors"
        >
          First
        </button>

        <button
          type="button"
          disabled={pageNumber <= 1}
          onClick={() => onPageChange(pageNumber - 1)}
          className="px-2.5 py-1 border border-gray-300 rounded bg-white text-gray-700 font-medium disabled:opacity-40 disabled:cursor-not-allowed hover:bg-gray-50 transition-colors"
        >
          Prev
        </button>

        {getPageNumbers().map((p, idx) =>
          typeof p === 'number' ? (
            <button
              key={p}
              type="button"
              onClick={() => onPageChange(p)}
              className={`px-3 py-1 font-medium rounded border transition-colors ${
                pageNumber === p
                  ? 'bg-slate-800 text-white border-slate-800'
                  : 'bg-white text-gray-700 border-gray-300 hover:bg-gray-50'
              }`}
            >
              {p}
            </button>
          ) : (
            <span key={`ellipsis-${idx}`} className="px-1 text-gray-400 font-bold">
              {p}
            </span>
          )
        )}

        <button
          type="button"
          disabled={pageNumber >= totalPages}
          onClick={() => onPageChange(pageNumber + 1)}
          className="px-2.5 py-1 border border-gray-300 rounded bg-white text-gray-700 font-medium disabled:opacity-40 disabled:cursor-not-allowed hover:bg-gray-50 transition-colors"
        >
          Next
        </button>

        <button
          type="button"
          disabled={pageNumber >= totalPages}
          onClick={() => onPageChange(totalPages)}
          className="px-2.5 py-1 border border-gray-300 rounded bg-white text-gray-700 font-medium disabled:opacity-40 disabled:cursor-not-allowed hover:bg-gray-50 transition-colors"
        >
          Last
        </button>
      </div>
    </div>
  );
};

```

---

### 4.7 Add / Edit Handover Modal (`src/features/SS/handover/components/HandoverModal.tsx`)

Contains operational form sections, field validations, popover intervention search, auto-scroll mechanisms, and inline status feedback banners.

```tsx
import React, { useState, useEffect, useRef } from 'react';
import { HandoverDto, SaveHandoverDto, DropdownOptionDto } from '@/lib/types/strategicStorage';
import { getHandoverUsers, getInterventionsLookup } from '@/lib/api/strategicStorage';

export interface HandoverModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (payload: SaveHandoverDto) => Promise<void>;
  isSubmitting: boolean;
  initialData?: HandoverDto | null;
}

const getLoggedInUserFromLocalStorage = (): { id: number; name: string } => {
  try {
    const rawAuth = localStorage.getItem('auth-storage');
    if (rawAuth) {
      const parsed = JSON.parse(rawAuth);
      const user = parsed?.state?.user;
      if (user) {
        return {
          id: user.id || 0,
          name: user.displayName || user.username || 'Current User',
        };
      }
    }
  } catch (err) {
    console.error('Error parsing auth-storage:', err);
  }
  return { id: 0, name: 'Current User' };
};

export const HandoverModal: React.FC<HandoverModalProps> = ({
  isOpen,
  onClose,
  onSubmit,
  isSubmitting,
  initialData,
}) => {
  const modalContainerRef = useRef<HTMLDivElement>(null);
  const getTodayIso = (): string => new Date().toISOString().split('T')[0];

  const [currentUser, setCurrentUser] = useState<{ id: number; name: string }>(
    getLoggedInUserFromLocalStorage()
  );

  const [handoverDate, setHandoverDate] = useState<string>(getTodayIso());
  const [standbyUserId, setStandbyUserId] = useState<string>('');
  const [predictedToAchieveTarget, setPredictedToAchieveTarget] = useState<string>('');
  const [allReservoirsOnTarget, setAllReservoirsOnTarget] = useState<string>('');
  const [hasNewRestrictions, setHasNewRestrictions] = useState<string>('');
  const [hasEnergyManagement, setHasEnergyManagement] = useState<string>('');
  const [gsosMytheLinkMainFlush, setGsosMytheLinkMainFlush] = useState<string>('');
  const [hasPlannedInterventions, setHasPlannedInterventions] = useState<string>('');
  const [selectedInterventions, setSelectedInterventions] = useState<DropdownOptionDto[]>([]);

  const [interventionSearchText, setInterventionSearchText] = useState<string>('');
  const [isInterventionDropdownOpen, setIsInterventionDropdownOpen] = useState<boolean>(false);

  const [thirdHighLiftMode, setThirdHighLiftMode] = useState<'Number' | 'Text'>('Number');
  const [thirdHighLiftHours, setThirdHighLiftHours] = useState<string>('');
  const [thirdHighLiftText, setThirdHighLiftText] = useState<string>('');
  const [notes, setNotes] = useState<string>('');

  const [standbyUsers, setStandbyUsers] = useState<DropdownOptionDto[]>([]);
  const [availableInterventions, setAvailableInterventions] = useState<DropdownOptionDto[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const scrollToTop = (): void => {
    if (modalContainerRef.current) {
      modalContainerRef.current.scrollTo({ top: 0, behavior: 'smooth' });
    }
  };

  const resetForm = (): void => {
    setHandoverDate(getTodayIso());
    setStandbyUserId('');
    setPredictedToAchieveTarget('');
    setAllReservoirsOnTarget('');
    setHasNewRestrictions('');
    setHasEnergyManagement('');
    setGsosMytheLinkMainFlush('');
    setHasPlannedInterventions('');
    setThirdHighLiftMode('Number');
    setThirdHighLiftHours('');
    setThirdHighLiftText('');
    setNotes('');
    setSelectedInterventions([]);
    setInterventionSearchText('');
    setIsInterventionDropdownOpen(false);
    setErrors({});
    setApiError(null);
    setSuccessMsg(null);
  };

  useEffect(() => {
    if (isOpen) {
      setErrors({});
      setApiError(null);
      setSuccessMsg(null);
      setCurrentUser(getLoggedInUserFromLocalStorage());

      Promise.all([getHandoverUsers(), getInterventionsLookup()]).then(([users, interventions]) => {
        const validUsers = (users || []).filter(
          (u) => u.name && u.name.trim().length > 0 && u.name.trim().toUpperCase() !== 'NULL'
        );
        setStandbyUsers(validUsers);
        setAvailableInterventions(interventions || []);

        if (initialData) {
          setHandoverDate(
            initialData.handoverDate ? initialData.handoverDate.split('T')[0] : getTodayIso()
          );
          setStandbyUserId(String(initialData.standbyUserId));
          setPredictedToAchieveTarget(initialData.predictedToAchieveTarget ? 'Yes' : 'No');
          setAllReservoirsOnTarget(initialData.allReservoirsOnTarget ? 'Yes' : 'No');
          setHasNewRestrictions(initialData.hasNewRestrictions ? 'Yes' : 'No');
          setHasEnergyManagement(initialData.hasEnergyManagement ? 'Yes' : 'No');
          setGsosMytheLinkMainFlush(initialData.gsosMytheLinkMainFlush);
          setHasPlannedInterventions(initialData.hasPlannedInterventions ? 'Yes' : 'No');

          setThirdHighLiftMode(initialData.thirdHighLiftRequirementMode || 'Number');
          setThirdHighLiftHours(
            initialData.thirdHighLiftHours !== undefined ? String(initialData.thirdHighLiftHours) : ''
          );
          setThirdHighLiftText(initialData.thirdHighLiftRequirementText || '');
          setNotes(initialData.notes || '');

          const preselected = (interventions || []).filter((i) =>
            initialData.interventionIds?.includes(Number(i.id))
          );
          setSelectedInterventions(preselected);
        } else {
          resetForm();
        }
      });
    }
  }, [isOpen, initialData]);

  if (!isOpen) return null;

  const validateForm = (): boolean => {
    const errs: Record<string, string> = {};

    if (!handoverDate) errs.handoverDate = 'Date is required';
    if (!standbyUserId) errs.standbyUserId = 'Standby contact is required';
    if (!predictedToAchieveTarget) errs.predictedToAchieveTarget = 'Predicted target achievement selection is required';
    if (!allReservoirsOnTarget) errs.allReservoirsOnTarget = 'All Reservoirs on target selection is required';
    if (!hasNewRestrictions) errs.hasNewRestrictions = 'New restrictions selection is required';
    if (!hasEnergyManagement) errs.hasEnergyManagement = 'Energy management selection is required';
    if (!gsosMytheLinkMainFlush) errs.gsosMytheLinkMainFlush = 'GSOS/Mythe Link Main Flush selection is required';
    if (!hasPlannedInterventions) errs.hasPlannedInterventions = 'Planned interventions selection is required';

    if (hasPlannedInterventions !== 'No' && selectedInterventions.length === 0) {
      errs.selectedInterventions = 'Interventions scheduled within next 72 hours is required';
    }

    if (thirdHighLiftMode === 'Number' && !thirdHighLiftHours.trim()) {
      errs.thirdHighLiftHours = 'Highlift required hours is required';
    }
    if (thirdHighLiftMode === 'Text' && !thirdHighLiftText.trim()) {
      errs.thirdHighLiftText = 'Highlift requirement details are required';
    }

    if (!notes.trim()) errs.notes = 'Notes field is required';

    setErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const clearFieldError = (key: string): void => {
    setErrors((prev) => {
      const updated = { ...prev };
      delete updated[key];
      return updated;
    });
  };

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>): Promise<void> => {
    e.preventDefault();
    setApiError(null);
    setSuccessMsg(null);
    scrollToTop();

    if (!validateForm()) return;

    const payload: SaveHandoverDto = {
      handoverId: initialData?.handoverId,
      handoverDate,
      handoverFromUserId: currentUser.id,
      standbyUserId: Number(standbyUserId),
      predictedToAchieveTarget: predictedToAchieveTarget === 'Yes',
      allReservoirsOnTarget: allReservoirsOnTarget === 'Yes',
      hasNewRestrictions: hasNewRestrictions === 'Yes',
      hasEnergyManagement: hasEnergyManagement === 'Yes',
      hasPlannedInterventions: hasPlannedInterventions === 'Yes',
      gsosMytheLinkMainFlush: gsosMytheLinkMainFlush as SaveHandoverDto['gsosMytheLinkMainFlush'],
      thirdHighLiftRequirementMode: thirdHighLiftMode,
      thirdHighLiftHours: thirdHighLiftMode === 'Number' ? Number(thirdHighLiftHours) : undefined,
      thirdHighLiftRequirementText: thirdHighLiftMode === 'Text' ? thirdHighLiftText.trim() : undefined,
      notes: notes.trim(),
      interventionIds: selectedInterventions.map((i) => Number(i.id)),
      createdBy: currentUser.id,
      modifiedBy: currentUser.id,
    };

    try {
      await onSubmit(payload);
      const msg = initialData ? 'Handover note updated successfully!' : 'Handover note added successfully!';
      setSuccessMsg(msg);
      scrollToTop();

      setTimeout(() => {
        resetForm();
        onClose();
      }, 2000);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Something went wrong. Please try again.';
      setApiError(msg);
      scrollToTop();
    }
  };

  const filteredInterventions = availableInterventions.filter((i) =>
    i.name.toLowerCase().includes(interventionSearchText.toLowerCase())
  );

  const activeErrors: string[] = Object.values(errors).filter((m): m is string => Boolean(m && m.trim()));

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-xs p-4">
      <div
        ref={modalContainerRef}
        className="bg-white rounded-xl shadow-2xl w-full max-w-3xl p-6 border border-gray-100 max-h-[92vh] overflow-y-auto relative text-xs"
      >
        {/* Header */}
        <div className="flex items-center justify-between pb-3 border-b border-gray-100 mb-4">
          <h2 className="text-lg font-bold text-gray-900">
            {initialData ? 'Edit Handover' : 'Add Handover'}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-lg p-1 rounded-full hover:bg-gray-100 transition-colors"
          >
            ✕
          </button>
        </div>

        {/* Success Banner */}
        {successMsg && (
          <div className="rounded-md border border-emerald-300 bg-emerald-50 text-emerald-800 p-4 mb-4 font-semibold text-sm flex items-center gap-2 shadow-xs">
            <span>✓</span>
            <span>{successMsg}</span>
          </div>
        )}

        {/* Error Summary Banner */}
        {(apiError || activeErrors.length > 0) && (
          <div className="rounded-md border border-destructive bg-destructive/10 p-4 mb-4 space-y-1">
            {apiError && <p className="text-sm font-medium text-destructive">{apiError}</p>}
            {activeErrors.length > 0 && (
              <div className="space-y-1">
                <p className="text-sm font-medium text-destructive">Please fix the following errors:</p>
                <ul className="list-disc list-inside text-sm text-destructive">
                  {activeErrors.map((msg, idx) => (
                    <li key={idx}>{msg}</li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Top Row */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div>
              <label className="block font-semibold text-gray-700 mb-1">
                <span>
                  Date
                  <span className="text-destructive font-bold ml-1">*</span> (auto populated)
                </span>
              </label>
              <input
                type="date"
                value={handoverDate}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => {
                  setHandoverDate(e.target.value);
                  if (e.target.value) clearFieldError('handoverDate');
                }}
                className={`w-full px-3 py-2 border rounded bg-white text-gray-800 font-medium focus:outline-none focus:ring-1 focus:ring-slate-700 ${
                  errors.handoverDate ? 'border-destructive' : 'border-gray-300'
                }`}
              />
            </div>

            <div>
              <label className="block font-semibold text-gray-700 mb-1">
                <span>
                  Handover From
                  <span className="text-destructive font-bold ml-1">*</span> (you)
                </span>
              </label>
              <input
                type="text"
                disabled
                value={currentUser.name}
                className="w-full px-3 py-2 border border-gray-300 rounded bg-gray-100 text-gray-600 font-medium"
              />
            </div>

            <div>
              <label className="block font-semibold text-gray-700 mb-1">
                <span>
                  Standby
                  <span className="text-destructive font-bold ml-1">*</span>
                </span>
              </label>
              <select
                value={standbyUserId}
                onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                  setStandbyUserId(e.target.value);
                  if (e.target.value) clearFieldError('standbyUserId');
                }}
                className={`w-full px-3 py-2 border rounded bg-white focus:outline-none focus:ring-1 focus:ring-slate-700 ${
                  errors.standbyUserId ? 'border-destructive' : 'border-gray-300'
                }`}
              >
                <option value="">Select Standby Contact</option>
                {standbyUsers.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
              {errors.standbyUserId && (
                <p className="text-xs text-destructive mt-1 font-medium">{errors.standbyUserId}</p>
              )}
            </div>
          </div>

          {/* Operational Status Section */}
          <div className="border-t pt-3 space-y-2">
            <h3 className="font-bold text-emerald-800 text-xs mb-1">Operational Status</h3>
            <div className="grid grid-cols-1 sm:grid-cols-5 gap-3 items-end">
              <div className="flex flex-col justify-between h-full">
                <label className="block font-semibold text-gray-700 min-h-[36px] flex items-end mb-1 leading-tight">
                  <span>
                    Predicted to achieve 82% target tomorrow at 06:00?
                    <span className="text-destructive font-bold ml-1">*</span>
                  </span>
                </label>
                <select
                  value={predictedToAchieveTarget}
                  onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                    setPredictedToAchieveTarget(e.target.value);
                    if (e.target.value) clearFieldError('predictedToAchieveTarget');
                  }}
                  className={`w-full px-2.5 py-1.5 border rounded bg-white ${
                    errors.predictedToAchieveTarget ? 'border-destructive' : 'border-gray-300'
                  }`}
                >
                  <option value="">Select</option>
                  <option value="Yes">Yes</option>
                  <option value="No">No</option>
                </select>
              </div>

              <div className="flex flex-col justify-between h-full">
                <label className="block font-semibold text-gray-700 min-h-[36px] flex items-end mb-1 leading-tight">
                  <span>
                    All Reservoirs on Target?
                    <span className="text-destructive font-bold ml-1">*</span>
                  </span>
                </label>
                <select
                  value={allReservoirsOnTarget}
                  onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                    setAllReservoirsOnTarget(e.target.value);
                    if (e.target.value) clearFieldError('allReservoirsOnTarget');
                  }}
                  className={`w-full px-2.5 py-1.5 border rounded bg-white ${
                    errors.allReservoirsOnTarget ? 'border-destructive' : 'border-gray-300'
                  }`}
                >
                  <option value="">Select</option>
                  <option value="Yes">Yes</option>
                  <option value="No">No</option>
                </select>
              </div>

              <div className="flex flex-col justify-between h-full">
                <label className="block font-semibold text-gray-700 min-h-[36px] flex items-end mb-1 leading-tight">
                  <span>
                    New Restrictions
                    <span className="text-destructive font-bold ml-1">*</span>
                  </span>
                </label>
                <select
                  value={hasNewRestrictions}
                  onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                    setHasNewRestrictions(e.target.value);
                    if (e.target.value) clearFieldError('hasNewRestrictions');
                  }}
                  className={`w-full px-2.5 py-1.5 border rounded bg-white ${
                    errors.hasNewRestrictions ? 'border-destructive' : 'border-gray-300'
                  }`}
                >
                  <option value="">Select</option>
                  <option value="Yes">Yes</option>
                  <option value="No">No</option>
                </select>
              </div>

              <div className="flex flex-col justify-between h-full">
                <label className="block font-semibold text-gray-700 min-h-[36px] flex items-end mb-1 leading-tight">
                  <span>
                    Energy Management
                    <span className="text-destructive font-bold ml-1">*</span>
                  </span>
                </label>
                <select
                  value={hasEnergyManagement}
                  onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                    setHasEnergyManagement(e.target.value);
                    if (e.target.value) clearFieldError('hasEnergyManagement');
                  }}
                  className={`w-full px-2.5 py-1.5 border rounded bg-white ${
                    errors.hasEnergyManagement ? 'border-destructive' : 'border-gray-300'
                  }`}
                >
                  <option value="">Select</option>
                  <option value="Yes">Yes</option>
                  <option value="No">No</option>
                </select>
              </div>

              <div className="flex flex-col justify-between h-full">
                <label className="block font-semibold text-gray-700 min-h-[36px] flex items-end mb-1 leading-tight">
                  <span>
                    GSOS/Mythe Link Main Flush?
                    <span className="text-destructive font-bold ml-1">*</span>
                  </span>
                </label>
                <select
                  value={gsosMytheLinkMainFlush}
                  onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                    setGsosMytheLinkMainFlush(e.target.value);
                    if (e.target.value) clearFieldError('gsosMytheLinkMainFlush');
                  }}
                  className={`w-full px-2.5 py-1.5 border rounded bg-white ${
                    errors.gsosMytheLinkMainFlush ? 'border-destructive' : 'border-gray-300'
                  }`}
                >
                  <option value="">Select</option>
                  <option value="No">No</option>
                  <option value="Yes">Yes</option>
                  <option value="Yes, GSOS">Yes, GSOS</option>
                  <option value="Yes, Link Main">Yes, Link Main</option>
                </select>
              </div>
            </div>
          </div>

          {/* Planned Interventions Section */}
          <div className="border-t pt-3 space-y-2">
            <h3 className="font-bold text-emerald-800 text-xs mb-1">Planned Interventions</h3>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 items-start">
              <div>
                <label className="block font-semibold text-gray-700 mb-1 leading-tight">
                  <span>
                    Interventions Planned for Tomorrow or Weekend
                    <span className="text-destructive font-bold ml-1">*</span>
                  </span>
                </label>
                <select
                  value={hasPlannedInterventions}
                  onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                    const val = e.target.value;
                    setHasPlannedInterventions(val);
                    if (val) clearFieldError('hasPlannedInterventions');
                    if (val === 'No') clearFieldError('selectedInterventions');
                  }}
                  className={`w-full px-2.5 py-1.5 border rounded bg-white ${
                    errors.hasPlannedInterventions ? 'border-destructive' : 'border-gray-300'
                  }`}
                >
                  <option value="">Select</option>
                  <option value="Yes">Yes</option>
                  <option value="No">No</option>
                </select>
              </div>

              <div className="sm:col-span-2">
                <label className="block font-semibold text-gray-700 mb-1 leading-tight">
                  <span>
                    Interventions Scheduled Within Next 72 Hours
                    <span className="text-destructive font-bold ml-1">*</span>
                    <span
                      className="inline-flex items-center justify-center w-3.5 h-3.5 rounded-full border border-gray-400 text-[10px] text-gray-500 font-bold cursor-help ml-1 align-middle"
                      title="Search and attach interventions scheduled within the next 72 hours"
                    >
                      ℹ
                    </span>
                  </span>
                </label>

                <div
                  className={`flex flex-wrap items-center gap-2 border p-1.5 rounded min-h-[38px] bg-white relative ${
                    errors.selectedInterventions ? 'border-destructive' : 'border-gray-300'
                  }`}
                >
                  {selectedInterventions.map((item) => (
                    <span
                      key={item.id}
                      className="inline-flex items-center gap-1 bg-emerald-50 text-emerald-800 px-2 py-1 rounded border border-emerald-200 text-xs font-semibold"
                    >
                      {item.name}
                      <button
                        type="button"
                        onClick={() => {
                          const updated = selectedInterventions.filter((i) => i.id !== item.id);
                          setSelectedInterventions(updated);
                        }}
                        className="text-emerald-900 font-bold ml-1 hover:text-red-700"
                      >
                        ✕
                      </button>
                    </span>
                  ))}

                  <div className="relative inline-block">
                    <button
                      type="button"
                      onClick={() => setIsInterventionDropdownOpen(!isInterventionDropdownOpen)}
                      className="px-2.5 py-1 border border-emerald-700 rounded bg-white text-xs font-semibold text-emerald-800 hover:bg-emerald-50 focus:outline-none"
                    >
                      + Search and Add Intervention ▾
                    </button>

                    {isInterventionDropdownOpen && (
                      <div className="absolute left-0 top-full mt-1 w-72 bg-white border border-gray-200 rounded-md shadow-lg z-50 p-2 space-y-2">
                        <input
                          type="text"
                          value={interventionSearchText}
                          onChange={(e) => setInterventionSearchText(e.target.value)}
                          placeholder="Type ID or site name..."
                          className="w-full px-2 py-1 border border-gray-300 rounded text-xs focus:outline-none focus:ring-1 focus:ring-slate-700"
                          autoFocus
                        />
                        <div className="max-h-48 overflow-y-auto divide-y divide-gray-100">
                          {filteredInterventions.length > 0 ? (
                            filteredInterventions.map((item) => (
                              <button
                                key={item.id}
                                type="button"
                                onClick={() => {
                                  if (!selectedInterventions.some((i) => i.id === item.id)) {
                                    setSelectedInterventions([...selectedInterventions, item]);
                                    clearFieldError('selectedInterventions');
                                  }
                                  setIsInterventionDropdownOpen(false);
                                  setInterventionSearchText('');
                                }}
                                className="w-full text-left px-2 py-1.5 hover:bg-emerald-50 text-xs text-gray-800 font-medium truncate"
                              >
                                {item.name}
                              </button>
                            ))
                          ) : (
                            <div className="p-2 text-gray-400 text-center">No interventions found</div>
                          )}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
                {errors.selectedInterventions && (
                  <p className="text-xs text-destructive mt-1 font-medium">{errors.selectedInterventions}</p>
                )}
              </div>
            </div>
          </div>

          {/* 3rd Highlift Section */}
          <div className="border-t pt-3 space-y-2">
            <h3 className="font-bold text-emerald-800 text-xs mb-1">
              <span>
                No. Hours 3rd Highlift Required
                <span className="text-destructive font-bold ml-1">*</span>
              </span>
            </h3>
            <div className="flex items-center gap-4 text-xs font-medium text-gray-700">
              <span className="font-bold text-gray-800">Mode:</span>
              <label className="flex items-center gap-1.5 cursor-pointer">
                <input
                  type="radio"
                  name="highliftMode"
                  checked={thirdHighLiftMode === 'Number'}
                  onChange={() => {
                    setThirdHighLiftMode('Number');
                    clearFieldError('thirdHighLiftText');
                  }}
                  className="accent-emerald-800"
                />
                Number
              </label>
              <label className="flex items-center gap-1.5 cursor-pointer">
                <input
                  type="radio"
                  name="highliftMode"
                  checked={thirdHighLiftMode === 'Text'}
                  onChange={() => {
                    setThirdHighLiftMode('Text');
                    clearFieldError('thirdHighLiftHours');
                  }}
                  className="accent-emerald-800"
                />
                Text
              </label>
            </div>

            <div>
              {thirdHighLiftMode === 'Number' ? (
                <input
                  type="number"
                  value={thirdHighLiftHours}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => {
                    setThirdHighLiftHours(e.target.value);
                    if (e.target.value) clearFieldError('thirdHighLiftHours');
                  }}
                  placeholder="e.g. 3"
                  className={`w-32 px-3 py-2 border rounded focus:outline-none focus:ring-1 focus:ring-slate-700 ${
                    errors.thirdHighLiftHours ? 'border-destructive' : 'border-gray-300'
                  }`}
                />
              ) : (
                <input
                  type="text"
                  value={thirdHighLiftText}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => {
                    setThirdHighLiftText(e.target.value);
                    if (e.target.value) clearFieldError('thirdHighLiftText');
                  }}
                  placeholder="Enter text (e.g. Morning only, 24/7 monitoring required)"
                  className={`w-full px-3 py-2 border rounded focus:outline-none focus:ring-1 focus:ring-slate-700 ${
                    errors.thirdHighLiftText ? 'border-destructive' : 'border-gray-300'
                  }`}
                />
              )}
            </div>
            {errors.thirdHighLiftHours && (
              <p className="text-xs text-destructive mt-1 font-medium">{errors.thirdHighLiftHours}</p>
            )}
            {errors.thirdHighLiftText && (
              <p className="text-xs text-destructive mt-1 font-medium">{errors.thirdHighLiftText}</p>
            )}
          </div>

          {/* Notes Section */}
          <div className="border-t pt-3">
            <label className="block font-semibold text-gray-700 mb-1">
              <span>
                Notes
                <span className="text-destructive font-bold ml-1">*</span>
              </span>
            </label>
            <textarea
              rows={4}
              value={notes}
              onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => {
                setNotes(e.target.value);
                if (e.target.value.trim()) clearFieldError('notes');
              }}
              placeholder="Include details of any reds, walk through energy management plan, and note anything out of the ordinary."
              className={`w-full px-3 py-2 border rounded focus:outline-none focus:ring-1 focus:ring-slate-700 ${
                errors.notes ? 'border-destructive' : 'border-gray-300'
              }`}
            />
            {errors.notes && (
              <p className="text-xs text-destructive mt-1 font-medium">{errors.notes}</p>
            )}
          </div>

          {/* Footer */}
          <div className="flex flex-col sm:flex-row items-center justify-between gap-3 pt-4 border-t border-gray-100">
            <span className="text-[11px] text-gray-500">
              * All fields are required. You must complete all fields to save.
            </span>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={onClose}
                disabled={Boolean(successMsg)}
                className="px-4 py-2 border border-gray-300 rounded text-gray-700 hover:bg-gray-50 font-medium transition-colors disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSubmitting || Boolean(successMsg)}
                className="px-5 py-2 bg-emerald-900 text-white rounded font-medium hover:bg-emerald-950 transition-colors disabled:opacity-50"
              >
                {isSubmitting ? 'Saving...' : 'Save Handover'}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
};

export default HandoverModal;

```

---

### 4.8 Assign Handover To Modal (`src/features/SS/handover/components/AssignHandoverToModal.tsx`)

```tsx
import React, { useState, useEffect } from 'react';
import { DropdownOptionDto } from '@/lib/types/strategicStorage';
import { assignHandoverTo } from '@/lib/api/strategicStorage';

interface AssignHandoverToModalProps {
  isOpen: boolean;
  onClose: () => void;
  handoverId: number | null;
  currentHandoverToUserId?: number | null;
  userList: DropdownOptionDto[];
  onSuccess: () => void;
}

const getLoggedInUserIdFromStorage = (): number => {
  try {
    const rawAuth = localStorage.getItem('auth-storage');
    if (rawAuth) {
      const parsed = JSON.parse(rawAuth);
      const userId = parsed?.state?.user?.id;
      if (userId) return Number(userId);
    }
  } catch (err) {
    console.error('Failed to parse auth-storage for user ID:', err);
  }
  return 0;
};

export const AssignHandoverToModal: React.FC<AssignHandoverToModalProps> = ({
  isOpen,
  onClose,
  handoverId,
  currentHandoverToUserId,
  userList,
  onSuccess,
}) => {
  const [selectedUserId, setSelectedUserId] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setSelectedUserId(currentHandoverToUserId ? String(currentHandoverToUserId) : '');
      setErrorMsg(null);
      setSuccessMsg(null);
    }
  }, [isOpen, currentHandoverToUserId]);

  if (!isOpen || !handoverId) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedUserId) {
      setErrorMsg('Please select a recipient');
      return;
    }

    setIsSubmitting(true);
    setErrorMsg(null);
    setSuccessMsg(null);

    const activeUserId = getLoggedInUserIdFromStorage();

    try {
      await assignHandoverTo(handoverId, Number(selectedUserId), activeUserId);
      setSuccessMsg('Handover assigned successfully!');
      setIsSubmitting(false);

      setTimeout(() => {
        onSuccess();
        onClose();
      }, 2000);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to assign handover recipient.';
      setErrorMsg(msg);
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-xs p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-md p-6 border border-gray-100 text-xs relative">
        {/* Header */}
        <div className="flex items-center justify-between pb-3 border-b border-gray-100 mb-4">
          <h2 className="text-sm font-bold text-gray-900 tracking-wide uppercase">
            ASSIGN HANDOVER TO
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-base p-1 rounded-full"
          >
            ✕
          </button>
        </div>

        {/* Success Banner */}
        {successMsg && (
          <div className="p-3 mb-3 rounded border border-emerald-300 bg-emerald-50 text-emerald-800 font-semibold text-xs flex items-center gap-2">
            <span>✓</span>
            <span>{successMsg}</span>
          </div>
        )}

        {/* Error Banner */}
        {errorMsg && (
          <div className="p-3 mb-3 rounded border border-destructive bg-destructive/10 text-destructive font-medium text-xs">
            {errorMsg}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <p className="text-gray-600 mb-2 font-medium">
              Select a team member to assign responsibility for this handover:
            </p>
            <select
              value={selectedUserId}
              onChange={(e) => {
                setSelectedUserId(e.target.value);
                if (e.target.value) setErrorMsg(null);
              }}
              className="w-full px-3 py-2 border border-gray-300 rounded bg-white font-medium text-gray-800 focus:outline-none focus:ring-1 focus:ring-slate-700"
            >
              <option value="">-- Select Recipient --</option>
              {userList.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </select>
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t">
            <button
              type="button"
              onClick={onClose}
              disabled={Boolean(successMsg)}
              className="px-4 py-2 border border-gray-300 rounded text-gray-700 hover:bg-gray-50 font-medium transition-colors disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting || Boolean(successMsg)}
              className="px-4 py-2 bg-emerald-900 text-white rounded font-semibold hover:bg-emerald-950 transition-colors disabled:opacity-50 flex items-center gap-1.5"
            >
              <span>⊕</span>
              <span>{isSubmitting ? 'Assigning...' : 'Assign Handover To'}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

```

---

### 4.9 Page Orchestrator Component (`src/features/SS/HandoverNotes.tsx`)

Combines components, modal triggers, state, and auto-refresh logic.

```tsx
import React, { useState, useEffect } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { useHandovers } from './handover/hooks/useHandovers';
import { HandoverTable } from './handover/components/HandoverTable';
import { HandoverPagination } from './handover/components/HandoverPagination';
import { HandoverModal } from './handover/components/HandoverModal';
import { AssignHandoverToModal } from './handover/components/AssignHandoverToModal';
import { HandoverDto, SaveHandoverDto, DropdownOptionDto } from '@/lib/types/strategicStorage';
import { getHandoverUsers } from '@/lib/api/strategicStorage';

export const HandoverNotes: React.FC = () => {
  const {
    data,
    totalRecords,
    totalPages,
    isLoading,
    filters,
    setPage,
    setPageSize,
    createHandoverNote,
    isCreating,
    updateHandoverNote,
    isUpdating,
    refetch,
  } = useHandovers();

  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);
  const [selectedHandover, setSelectedHandover] = useState<HandoverDto | null>(null);

  const [isAssignModalOpen, setIsAssignModalOpen] = useState<boolean>(false);
  const [assignTargetHandover, setAssignTargetHandover] = useState<HandoverDto | null>(null);
  const [userList, setUserList] = useState<DropdownOptionDto[]>([]);

  useEffect(() => {
    getHandoverUsers().then((users) => {
      const validUsers = (users || []).filter(
        (u) => u.name && u.name.trim().length > 0 && u.name.trim().toUpperCase() !== 'NULL'
      );
      setUserList(validUsers);
    });
  }, []);

  const handleOpenAddModal = () => {
    setSelectedHandover(null);
    setIsModalOpen(true);
  };

  const handleOpenEditModal = (handover: HandoverDto) => {
    setSelectedHandover(handover);
    setIsModalOpen(true);
  };

  const handleOpenAssignModal = (handover: HandoverDto) => {
    setAssignTargetHandover(handover);
    setIsAssignModalOpen(true);
  };

  const handleSaveHandover = async (payload: SaveHandoverDto) => {
    if (payload.handoverId) {
      await updateHandoverNote({ id: payload.handoverId, payload });
    } else {
      await createHandoverNote(payload);
    }
  };

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-bold text-gray-900 tracking-tight">HANDOVER NOTES</h1>
          <p className="text-xs text-gray-500">
            Include details of any reds, walk through energy management plan, and note anything out of the ordinary.
          </p>
        </div>
        <button
          type="button"
          onClick={handleOpenAddModal}
          className="px-4 py-2 bg-emerald-800 text-white text-xs font-semibold rounded hover:bg-emerald-900 transition-colors flex items-center gap-1.5 shadow-sm"
        >
          <span>+</span> Add Handover
        </button>
      </div>

      <Card className="border border-gray-200 shadow-sm">
        <CardContent className="p-4 space-y-3">
          {/* Controls Bar */}
          <div className="flex items-center justify-between text-xs pb-1">
            <div className="flex items-center gap-2">
              <span className="font-medium text-gray-700">Records per page:</span>
              <select
                value={filters.pageSize}
                onChange={(e) => setPageSize(Number(e.target.value))}
                className="border border-gray-300 rounded px-2 py-1 bg-white font-medium text-gray-800 focus:outline-none focus:ring-1 focus:ring-slate-700"
              >
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={30}>30</option>
                <option value={50}>50</option>
              </select>
            </div>

            <div className="font-semibold text-gray-800">
              Total Records: <span className="text-slate-900">{totalRecords.toLocaleString()}</span>
            </div>
          </div>

          {/* Grid Table */}
          <HandoverTable
            data={data}
            isLoading={isLoading}
            onEdit={handleOpenEditModal}
            onAssignHandoverTo={handleOpenAssignModal}
          />

          {/* Pagination */}
          <HandoverPagination
            pageNumber={filters.pageNumber}
            pageSize={filters.pageSize}
            totalRecords={totalRecords}
            totalPages={totalPages}
            onPageChange={setPage}
          />
        </CardContent>
      </Card>

      {/* Add / Edit Handover Modal */}
      <HandoverModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSubmit={handleSaveHandover}
        isSubmitting={isCreating || isUpdating}
        initialData={selectedHandover}
      />

      {/* Assign Handover To Modal */}
      <AssignHandoverToModal
        isOpen={isAssignModalOpen}
        onClose={() => setIsAssignModalOpen(false)}
        handoverId={assignTargetHandover?.handoverId || null}
        currentHandoverToUserId={assignTargetHandover?.handoverToUserId}
        userList={userList}
        onSuccess={refetch}
      />
    </div>
  );
};

export default HandoverNotes;

```

---

## 5. Key Architectural & Layout Fixes Summary

Below is a reference of the layout and architectural fixes implemented across the stack:

| Technical Challenge | Root Cause | Implemented Architecture Fix |
| --- | --- | --- |
| **Flexbox Label Asterisk Separation** | `<label className="flex items-end">Text <span>*</span></label>` treated text and red asterisk as separate flex items, pushing `*` to the far-right edge. | Wrapped label text and the asterisk inside a single inner `<span>` tag (`<span>Text <span className="text-destructive">*</span></span>`), restoring standard inline HTML text wrapping. |
| **Planned Interventions Dropdown Vertical Shift** | Grid container used `items-end` combined with `justify-between h-full` on the left column. Adding multiple tags expanded the right container height, pushing the left dropdown down. | Changed grid alignment from `items-end` to **`items-start`** and removed `h-full` on the left column. This locks the left dropdown at the top level regardless of tag container expansion. |
| **Notes Column Vertical Compression** | Notes cell used `max-w-xs`, squeezing long paragraphs vertically into 15–20 lines and stretching table rows. | Replaced `max-w-xs` with `min-w-[380px] max-w-xl text-xs whitespace-pre-wrap leading-relaxed`. |
| **SQL Update Parameter Mismatch** | `StrategicStorageHandoverRepository.cs` sent `@HandoverDate`, but `[IMT].[SPStratStorUpdateHandover]` did not declare `@HandoverDate`, causing a `.NET 500 Exception`. | Added `@HandoverDate DATETIME = NULL` to `[IMT].[SPStratStorUpdateHandover]` and updated the `SET` clause (`HandoverDate = ISNULL(@HandoverDate, HandoverDate)`). |
| **Blank Entries in Standby Dropdown** | Underlying database procedure returns shortcodes with NULL full names for other application requirements. | Implemented frontend filtering (`users.filter(u => u.name && u.name.trim().length > 0 && u.name.toUpperCase() !== 'NULL')`), preventing blank options in the dropdown without modifying database stored procedures. |
| **Instant Toast Dismissal on Modal Unmount** | Toast notifications unmounted before completing animation cycles when modals closed immediately. | Replaced `react-toastify` with an in-modal standard banner system (`bg-emerald-50 border-emerald-300 text-emerald-800`), featuring auto-scrolling to top and a 2-second display delay before modal exit. |