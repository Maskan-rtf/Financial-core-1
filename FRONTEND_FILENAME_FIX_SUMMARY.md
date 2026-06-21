# Frontend Document Filename Fix - Summary

## Changes Applied

Updated all frontend document upload flows to pass the original filename to the backend's document confirmation endpoint.

### Files Modified

1. **Frontend/js/portal.js** - Investment case document uploads
   - Updated `uploadDocument()` to include `&originalFileName=` parameter

2. **Frontend/js/guarantee-portal.js** - Guarantee case document uploads
   - Updated `uploadDocument()` to include `&originalFileName=` parameter

3. **Frontend/js/loan-portal.js** - Loan case document uploads
   - Updated `uploadDocument()` to include `&originalFileName=` parameter

4. **Frontend/workflow-runner.js** - Automated workflow document uploads
   - Updated `uploadDocument()` to include `&originalFileName=` parameter

5. **Frontend/app.js** - Test/debug UI document uploads
   - Added `lastFileName` variable to store the original filename
   - Updated `btnConfirmUpload` handler to include `&originalFileName=` parameter
   - **Note**: Two legacy admin endpoints (manual S3 key entry) were intentionally NOT modified as they don't have file objects

### Implementation Pattern

All updated endpoints now follow this pattern:

```javascript
const encodedFileName = encodeURIComponent(file.name);
await apiRequest({
  method: "POST",
  path: `${basePath}/${caseId}/documents/confirm?s3Key=${encodeURIComponent(s3Key)}&originalFileName=${encodedFileName}`,
  body: null,
  json: false
});
```

### What This Fixes

- **Before**: All documents were saved with filenames like "1.jpg", "1.png", "1.pdf"
- **After**: Documents are saved with their original filenames like "contract-signed.pdf", "valuation-report.jpg"

### Backward Compatibility

The backend API is backward compatible:
- If `originalFileName` is provided → uses it
- If `originalFileName` is omitted → falls back to extracting from S3 key (legacy behavior)

### Testing Checklist

- [ ] Investment case document upload preserves filename
- [ ] Guarantee case document upload preserves filename
- [ ] Loan case document upload preserves filename
- [ ] Workflow runner document upload preserves filename
- [ ] Test UI document upload preserves filename
- [ ] Persian filenames work correctly
- [ ] Filenames with special characters work correctly
- [ ] Spaces in filenames work correctly

### URL Encoding

All implementations properly use `encodeURIComponent()` to handle:
- Persian/Unicode characters
- Spaces
- Special characters (%, &, =, etc.)
