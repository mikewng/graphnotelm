## Phase 5 — React Frontend on S3 + CloudFront

### 5.1 Build the React app

In your React repo, set the API base URL to your ALB domain or custom domain:

```bash
# .env.production
REACT_APP_API_URL=https://api.yourdomain.com
```

```bash
npm run build
```

### 5.2 Create an S3 bucket

```powershell
aws s3 mb s3://graphnotelm-frontend --region us-east-1

# Enable static website hosting
aws s3 website s3://graphnotelm-frontend --index-document index.html --error-document index.html

# Upload the build output
aws s3 sync ./build s3://graphnotelm-frontend --delete
```

### 5.3 Create a CloudFront distribution

- Go to **CloudFront → Create Distribution**
- Origin: your S3 bucket (use the S3 REST endpoint, **not** the website endpoint)
- Origin Access Control (OAC): Create one and update the S3 bucket policy to allow CloudFront only
- Viewer protocol: **Redirect HTTP to HTTPS**
- Default root object: `index.html`
- **Custom error pages:** Add a rule — `403 → /index.html, 200` (handles React Router client-side routing)
- Attach your ACM certificate for `yourdomain.com`

### 5.4 Update DNS

- Point `yourdomain.com` (or `www.yourdomain.com`) CNAME → CloudFront distribution domain
- Point `api.yourdomain.com` CNAME → ALB DNS name

---