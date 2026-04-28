import { useMyCertificates } from '../api/use-certificates'
import { Skeleton } from '@/components/feedback/Skeleton'
import { EmptyState } from '@/components/feedback/EmptyState'

export function MyCertificatesPage() {
  const { data: certificates, isLoading } = useMyCertificates()

  if (isLoading) return <Skeleton className="h-64 w-full" />

  return (
    <div>
      <h1 className="mb-6 text-2xl font-bold text-foreground">My Certificates</h1>
      {!certificates?.length ? (
        <EmptyState title="No certificates yet" description="Complete a course to earn a certificate." />
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {certificates.map(cert => (
            <div key={cert.id} className="rounded-lg border border-border p-4">
              <h2 className="font-medium text-foreground">{cert.courseTitle}</h2>
              <p className="text-sm text-muted-foreground">
                Issued: {new Date(cert.issuedAt).toLocaleDateString()}
              </p>
              <div className="mt-3 flex gap-3">
                <a href={cert.pdfUrl} target="_blank" rel="noopener noreferrer" className="text-sm text-primary hover:underline">
                  Download PDF
                </a>
                <a href={`/verify/${cert.verificationCode}`} className="text-sm text-muted-foreground hover:underline">
                  Verify
                </a>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
