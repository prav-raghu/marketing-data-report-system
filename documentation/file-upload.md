# File Upload System Documentation

## Overview

The file upload system provides unified storage capabilities across all backend services (customer-api, admin-api, schedule-api) with support for AWS S3, Azure Blob Storage, and Cloudflare R2. Features include image processing, signed URLs, file validation, and streaming uploads.

## Architecture

### Storage Package: `@node-mono-repo-template/storage`

**Multi-Provider Support:**
- AWS S3 (with SDK v3)
- Azure Blob Storage
- Cloudflare R2 (S3-compatible, dual-bucket public/private model)
- Extensible for additional providers

**Core Services:**
- `S3StorageService`: AWS S3 integration
- `AzureBlobStorageService`: Azure Blob integration
- `R2StorageService`: Cloudflare R2 integration
- `StorageService`: Unified interface with image processing

---

## Configuration

### Environment Variables

**AWS S3:**
```env
STORAGE_PROVIDER=S3
AWS_ACCESS_KEY_ID=your_access_key
AWS_SECRET_ACCESS_KEY=your_secret_key
AWS_REGION=us-east-1
AWS_S3_BUCKET=my-app-bucket
```

**Azure Blob:**
```env
STORAGE_PROVIDER=AZURE_BLOB
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=...
AZURE_STORAGE_CONTAINER=my-app-container
```

**Cloudflare R2:**
```env
STORAGE_PROVIDER=R2
R2_ACCOUNT_ID=your_account_id
R2_ACCESS_KEY_ID=your_access_key
R2_SECRET_ACCESS_KEY=your_secret_key
R2_PUBLIC_BUCKET=my-app-public
R2_PUBLIC_BASE_URL=https://cdn.example.com
R2_PRIVATE_BUCKET=my-app-private
```

### Service Initialization

```typescript
import { StorageService, StorageProvider } from '@node-mono-repo-template/storage';

// S3 Configuration
const storageService = new StorageService({
  provider: StorageProvider.S3,
  s3: {
    region: process.env.AWS_REGION!,
    accessKeyId: process.env.AWS_ACCESS_KEY_ID!,
    secretAccessKey: process.env.AWS_SECRET_ACCESS_KEY!,
    defaultBucket: process.env.AWS_S3_BUCKET,
  },
});

// Azure Blob Configuration
const storageService = new StorageService({
  provider: StorageProvider.AZURE_BLOB,
  azureBlob: {
    connectionString: process.env.AZURE_STORAGE_CONNECTION_STRING!,
    defaultContainer: process.env.AZURE_STORAGE_CONTAINER,
  },
});
```

---

## File Upload

### Basic Upload

```typescript
import { StorageService, FileAccessLevel } from '@node-mono-repo-template/storage';

const result = await storageService.uploadFile(buffer, {
  folder: 'documents',
  fileName: 'contract.pdf',
  contentType: 'application/pdf',
  maxSizeBytes: 10 * 1024 * 1024, // 10 MB
  allowedMimeTypes: ['application/pdf', 'application/msword'],
  accessLevel: FileAccessLevel.PRIVATE,
  metadata: {
    userId: 'user123',
    uploadedBy: 'admin',
    department: 'legal',
  },
});

console.log(result);
// {
//   id: 'documents/contract.pdf',
//   fileName: 'contract.pdf',
//   originalName: 'contract.pdf',
//   mimeType: 'application/pdf',
//   sizeBytes: 524288,
//   url: 'https://bucket.s3.amazonaws.com/documents/contract.pdf',
//   provider: 'S3',
//   bucket: 'my-app-bucket',
//   key: 'documents/contract.pdf',
//   uploadedAt: '2025-12-03T12:00:00Z',
//   metadata: { userId: 'user123', ... }
// }
```

### Image Upload with Processing

```typescript
const result = await storageService.uploadImage(imageBuffer, {
  folder: 'profile-images',
  fileName: 'avatar.jpg',
  resize: { 
    width: 300, 
    height: 300 
  },
  format: 'webp',
  quality: 85,
  contentType: 'image/webp',
  accessLevel: FileAccessLevel.PUBLIC,
});
```

**Image Processing Features:**
- **Resize**: Proportional resizing with max dimensions
- **Format Conversion**: JPEG, PNG, WebP, GIF
- **Quality Control**: Compression level (1-100)
- **Sharp Integration**: High-performance image processing

---

## Upload Options

```typescript
interface UploadOptions {
  bucket?: string;                  // Override default bucket/container
  folder?: string;                  // Organize files in folders
  fileName?: string;                // Custom file name (auto-generated if not provided)
  contentType?: string;             // MIME type
  maxSizeBytes?: number;            // Maximum file size
  allowedMimeTypes?: string[];      // Whitelist of allowed types
  accessLevel?: FileAccessLevel;    // PUBLIC, PRIVATE, AUTHENTICATED
  metadata?: Record<string, string>; // Custom metadata
}
```

### Access Levels

| Level | S3 ACL | Azure Access | R2 Routing | Use Case |
|-------|--------|--------------|------------|----------|
| `PUBLIC` | `public-read` | Public container | Public bucket → CDN URL | Public assets, logos |
| `PRIVATE` | `private` | Private container | Private bucket → presigned URL | User documents, sensitive files |
| `AUTHENTICATED` | `authenticated-read` | SAS tokens | Private bucket → presigned URL | Member-only content |

> **R2 note:** R2 has no per-object ACLs. The provider routes uploads to a separate public or private bucket based on `accessLevel`. Public objects are served from the public bucket's CDN/custom domain (`url` + `isPublic: true`); private objects are reachable only via presigned URLs from the private bucket.

---

## Signed URLs (Temporary Access)

### Generate Signed URL

```typescript
const signedUrl = await storageService.getSignedUrl(
  'documents/contract.pdf',
  undefined, // Use default bucket
  {
    expiresIn: 3600, // 1 hour
    responseContentType: 'application/pdf',
    responseContentDisposition: 'attachment; filename="contract.pdf"',
  }
);

console.log(signedUrl);
// {
//   url: 'https://bucket.s3.amazonaws.com/documents/contract.pdf?X-Amz-Signature=...',
//   expiresAt: '2025-12-03T13:00:00Z'
// }
```

**Use Cases:**
- Secure download links
- Time-limited file access
- Direct browser downloads
- Email attachment links

---

## File Validation

### Validate File Type

```typescript
const isValid = storageService.validateFileType(
  'document.pdf',
  ['application/pdf', 'application/msword']
);

if (!isValid) {
  throw new Error('File type not allowed');
}
```

### Validate File Size

```typescript
const maxSize = 10 * 1024 * 1024; // 10 MB
const isValid = storageService.validateFileSize(fileSize, maxSize);

if (!isValid) {
  throw new Error('File size exceeds maximum allowed size');
}
```

---

## Fastify Integration

### Controller Example (Customer API - Profile Images)

```typescript
import { FastifyRequest, FastifyReply } from 'fastify';
import { StorageService, FileAccessLevel } from '@node-mono-repo-template/storage';

export class UploadController {
  constructor(private readonly storageService: StorageService) {}

  public async uploadProfileImage(req: FastifyRequest, reply: FastifyReply): Promise<FastifyReply> {
    try {
      const data = await req.file();
      
      if (!data) {
        return reply.status(400).send({ success: false, error: 'No file provided' });
      }

      // Validate file type
      if (!this.storageService.validateFileType(data.filename, ['image/jpeg', 'image/png', 'image/webp'])) {
        return reply.status(400).send({ success: false, error: 'Invalid file type' });
      }

      // Read buffer
      const buffer = await data.toBuffer();

      // Validate size (5 MB max)
      if (!this.storageService.validateFileSize(buffer.length, 5 * 1024 * 1024)) {
        return reply.status(400).send({ success: false, error: 'File size exceeds 5 MB' });
      }

      // Upload with processing
      const result = await this.storageService.uploadImage(buffer, {
        folder: `profile-images/${req.user.id}`,
        fileName: `avatar-${Date.now()}.webp`,
        resize: { width: 300, height: 300 },
        format: 'webp',
        quality: 85,
        accessLevel: FileAccessLevel.PUBLIC,
        metadata: {
          userId: req.user.id,
          originalName: data.filename,
        },
      });

      return reply.send({
        success: true,
        data: result,
      });
    } catch (error) {
      return reply.status(500).send({
        success: false,
        error: error instanceof Error ? error.message : 'Upload failed',
      });
    }
  }

  public async downloadFile(req: FastifyRequest, reply: FastifyReply): Promise<FastifyReply> {
    try {
      const { fileKey } = req.params as { fileKey: string };

      const signedUrl = await this.storageService.getSignedUrl(fileKey, undefined, {
        expiresIn: 3600,
      });

      return reply.redirect(signedUrl.url);
    } catch (error) {
      return reply.status(500).send({
        success: false,
        error: 'Failed to generate download link',
      });
    }
  }
}
```

### Routes

```typescript
import { FastifyInstance } from 'fastify';
import { UploadController } from '../controllers/upload.controller';

export class UploadRoutes {
  constructor(
    private readonly fastify: FastifyInstance,
    private readonly controller: UploadController
  ) {}

  public register(): void {
    this.fastify.post(
      '/upload/profile-image',
      {
        schema: {
          tags: ['Upload'],
          description: 'Upload profile image',
          consumes: ['multipart/form-data'],
        },
      },
      this.controller.uploadProfileImage.bind(this.controller)
    );

    this.fastify.get(
      '/download/:fileKey',
      {
        schema: {
          tags: ['Upload'],
          description: 'Download file with signed URL',
        },
      },
      this.controller.downloadFile.bind(this.controller)
    );
  }
}
```

### Multipart Support

Add `@fastify/multipart` plugin:

```bash
pnpm add @fastify/multipart
```

Register in `application.ts`:

```typescript
import multipart from '@fastify/multipart';

await this.app.register(multipart, {
  limits: {
    fileSize: 10 * 1024 * 1024, // 10 MB
    files: 1,
  },
});
```

---

## Service-Specific Use Cases

### Customer API
- **Profile images**: Avatar uploads with resize
- **Documents**: KYC documents, ID verification
- **Support tickets**: Attachment uploads

### Admin API
- **Bulk imports**: CSV/Excel file uploads for data import
- **Media library**: Logo, banner, marketing assets
- **Report uploads**: PDF reports, data exports

### Schedule API
- **Job attachments**: Files associated with scheduled jobs
- **Execution logs**: Log file uploads
- **Backups**: Database backup file storage

---

## Security Best Practices

### 1. File Type Validation
Always validate MIME types:

```typescript
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
const ALLOWED_DOCUMENT_TYPES = ['application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'];

if (!storageService.validateFileType(fileName, ALLOWED_IMAGE_TYPES)) {
  throw new Error('Invalid file type');
}
```

### 2. File Size Limits

```typescript
const MAX_SIZES = {
  IMAGE: 5 * 1024 * 1024,      // 5 MB
  DOCUMENT: 10 * 1024 * 1024,  // 10 MB
  VIDEO: 100 * 1024 * 1024,    // 100 MB
};
```

### 3. Virus Scanning (Recommended)

Integrate ClamAV or cloud-based scanning:

```typescript
async function scanFile(buffer: Buffer): Promise<boolean> {
  // Integrate with virus scanning service
  const isClean = await virusScanService.scan(buffer);
  return isClean;
}
```

### 4. Rate Limiting

```typescript
// In application.ts
await this.app.register(rateLimit, {
  max: 10,
  timeWindow: '15 minutes',
  // Apply to upload routes
  skipOnError: false,
});
```

### 5. Authentication & Authorization

```typescript
// Verify user owns the resource
if (fileMetadata.userId !== req.user.id && req.user.role !== 'ADMIN') {
  return reply.status(403).send({ error: 'Forbidden' });
}
```

### 6. Signed URLs Instead of Direct Access

```typescript
// ❌ Don't expose direct S3 URLs
const url = 'https://bucket.s3.amazonaws.com/private/file.pdf';

// ✅ Use signed URLs with expiration
const signedUrl = await storageService.getSignedUrl('private/file.pdf', undefined, {
  expiresIn: 3600,
});
```

---

## Error Handling

### Common Errors

**File Too Large:**
```typescript
try {
  await storageService.uploadFile(buffer, options);
} catch (error) {
  if (error.message.includes('exceeds maximum allowed size')) {
    return reply.status(413).send({ error: 'File too large' });
  }
}
```

**Invalid File Type:**
```typescript
if (error.message.includes('is not allowed')) {
  return reply.status(415).send({ error: 'Unsupported file type' });
}
```

**Upload Failed:**
```typescript
catch (error) {
  logger.error('File upload failed', { error, userId: req.user.id });
  return reply.status(500).send({ error: 'Upload failed' });
}
```

---

## Performance Optimization

### 1. Streaming Uploads (Large Files)

```typescript
import { pipeline } from 'node:stream/promises';

async function uploadLargeFile(req: FastifyRequest) {
  const data = await req.file();
  const stream = data.file;
  
  await storageService.uploadFile(stream, options);
}
```

### 2. Parallel Uploads

```typescript
const uploads = files.map(file => 
  storageService.uploadFile(file.buffer, {
    folder: 'batch-upload',
    fileName: file.name,
  })
);

const results = await Promise.all(uploads);
```

### 3. Image Optimization

```typescript
// Use WebP format for 25-35% smaller file sizes
const result = await storageService.uploadImage(buffer, {
  format: 'webp',
  quality: 80, // Balance quality and size
  resize: { width: 1920 }, // Limit max dimensions
});
```

### 4. CDN Integration

Use CloudFront (S3) or Azure CDN for faster delivery:

```typescript
const cdnUrl = `https://cdn.example.com/${result.key}`;
```

---

## Monitoring & Logging

### Log Upload Events

```typescript
logger.info('File uploaded', {
  userId: req.user.id,
  fileName: result.fileName,
  fileSize: result.sizeBytes,
  provider: result.provider,
  duration: Date.now() - startTime,
});
```

### Metrics to Track

1. **Upload Success Rate**: Success vs failure ratio
2. **Average Upload Time**: By file size and type
3. **Storage Usage**: Total bytes stored per user/tenant
4. **Bandwidth**: Upload/download bandwidth usage
5. **Error Rates**: By error type

---

## Testing

### Unit Tests

```typescript
import { StorageService, StorageProvider } from '@node-mono-repo-template/storage';

describe('StorageService', () => {
  let storageService: StorageService;

  beforeEach(() => {
    storageService = new StorageService({
      provider: StorageProvider.S3,
      s3: {
        region: 'us-east-1',
        accessKeyId: 'test-key',
        secretAccessKey: 'test-secret',
        defaultBucket: 'test-bucket',
      },
    });
  });

  it('should validate file types', () => {
    expect(storageService.validateFileType('image.jpg', ['image/jpeg'])).toBe(true);
    expect(storageService.validateFileType('document.pdf', ['image/jpeg'])).toBe(false);
  });

  it('should validate file sizes', () => {
    expect(storageService.validateFileSize(1000, 2000)).toBe(true);
    expect(storageService.validateFileSize(3000, 2000)).toBe(false);
  });
});
```

---

## Migration Guide

### From Local Storage to Cloud

```typescript
// Before (local filesystem)
import fs from 'fs/promises';
await fs.writeFile('/uploads/file.pdf', buffer);

// After (cloud storage)
await storageService.uploadFile(buffer, {
  folder: 'uploads',
  fileName: 'file.pdf',
});
```

### Provider Migration (S3 to Azure)

1. Update environment variables
2. Change `StorageProvider` configuration
3. Run data migration script
4. Update DNS/CDN configuration

---

## Cost Optimization

### AWS S3
- Use S3 Intelligent-Tiering for automatic cost optimization
- Enable lifecycle policies to move old files to Glacier
- Use S3 Transfer Acceleration only when needed

### Azure Blob
- Use Cool/Archive tiers for infrequently accessed files
- Enable soft delete for 30 days (compliance)
- Use reserved capacity for predictable workloads

### General
- Compress files before upload
- Delete old/unused files regularly
- Use CDN to reduce origin requests
- Monitor and alert on unexpected usage spikes

---

## Future Enhancements

1. **Multi-Provider Fallback**: Automatic failover between providers
2. **Chunked Uploads**: Support for resumable uploads
3. **Image Thumbnails**: Automatic generation of multiple sizes
4. **Video Processing**: Transcoding and thumbnail generation
5. **Direct Browser Uploads**: Pre-signed POST policies
6. **File Versioning**: Keep multiple versions of files
7. **Encryption at Rest**: Customer-managed encryption keys
8. **Content Moderation**: Automatic content scanning (AI)
