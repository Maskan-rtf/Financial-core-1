# Document Filename Bug Fix

## Problem
All uploaded documents were being saved with filename "1.{ext}" instead of preserving the original filename.

### Root Cause
When using the presigned URL upload flow:
1. Client gets presigned URL with S3 key: `cases/{caseNumber}/{documentType}/{version}{ext}`
   - Example: `cases/12345/1/1.jpg`
2. Client uploads file directly to S3
3. `ConfirmDocumentUploadedAsync` is called with only the `s3Key` parameter
4. The method extracted filename using `Path.GetFileName(s3Key)` → returns `"1.jpg"`
5. Original filename (e.g., `"contract-signed.jpg"`) was lost

## Solution
Added an optional `originalFileName` parameter to the document confirmation endpoints.

### API Changes

**Investment Cases:**
```http
POST /api/v1/investmentcases/{id}/documents/confirm?s3Key={key}&originalFileName={name}
```

**Guarantee Cases:**
```http
POST /api/v1/guaranteecases/{id}/documents/confirm?s3Key={key}&originalFileName={name}
```

**Loan Cases:**
```http
POST /api/v1/loancases/{id}/documents/confirm?s3Key={key}&originalFileName={name}
```

### Parameters
- `s3Key` (required): The S3 key returned from the presigned upload URL
- `originalFileName` (optional): The original filename from the user's file picker
  - If provided: stored in the database
  - If omitted: falls back to extracting filename from S3 key (backward compatible)

## Frontend Integration

### Before (Bug):
```javascript
// Client uploads file to presigned URL
await fetch(presignedUrl, {
  method: 'PUT',
  body: file,
  headers: { 'Content-Type': file.type }
});

// Confirm upload (filename lost)
await apiRequest('POST', `/investmentcases/${caseId}/documents/confirm?s3Key=${s3Key}`);
```

### After (Fixed):
```javascript
// Client uploads file to presigned URL
await fetch(presignedUrl, {
  method: 'PUT',
  body: file,
  headers: { 'Content-Type': file.type }
});

// Confirm upload with original filename
const encodedFileName = encodeURIComponent(file.name);
await apiRequest('POST', 
  `/investmentcases/${caseId}/documents/confirm?s3Key=${s3Key}&originalFileName=${encodedFileName}`
);
```

### Important Notes
1. **URL Encode**: Always URL-encode the filename to handle special characters, spaces, and Persian text
2. **Backward Compatible**: The API remains backward compatible - existing code without `originalFileName` will still work (but continue to show version-based names)
3. **All Case Types**: Update for Investment, Guarantee, and Loan cases

## Files Modified
- `Core.API/Controllers/InvestmentCasesController.cs`
- `Core.API/Controllers/GuaranteeCasesController.cs`
- `Core.API/Controllers/LoanCasesController.cs`
- `Core.Application/Abstractions/IInvestmentCaseAppService.cs`
- `Core.Application/Abstractions/IGuaranteeCaseAppService.cs`
- `Core.Application/Abstractions/ILoanCaseAppService.cs`
- `Core.Application/Services/InvestmentCaseAppService.cs`
- `Core.Application/Services/GuaranteeCaseAppService.cs`
- `Core.Application/Services/LoanCaseAppService.cs`

## Testing
1. Upload a document with a descriptive filename (e.g., `"contract-final-signed.pdf"`)
2. Verify the filename is preserved in the database and returned in the documents list
3. Test with special characters and Persian filenames
4. Verify backward compatibility by omitting the `originalFileName` parameter
