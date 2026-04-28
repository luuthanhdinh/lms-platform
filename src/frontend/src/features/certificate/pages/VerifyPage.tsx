import { useVerifyCertificate } from '../api/use-certificates'
import { Skeleton } from '@/components/feedback/Skeleton'

interface Props {
  code: string
}

export function VerifyPage({ code }: Props) {
  const { data, isLoading } = useVerifyCertificate(code)

  if (isLoading) return <Skeleton className="h-48 w-full" />

  return (
    <div className="mx-auto max-w-xl text-center">
      <h1 className="mb-6 text-3xl font-bold text-foreground">Certificate Verification</h1>
      {data?.isValid ? (
        <div className="rounded-lg border border-green-200 bg-green-50 p-6">
          <p className="mb-2 text-green-700 font-semibold">Certificate is valid</p>
          {data.certificate && (
            <div className="text-sm text-green-600">
              <p>{data.certificate.learnerName}</p>
              <p>{data.certificate.courseTitle}</p>
              <p>Issued: {new Date(data.certificate.issuedAt).toLocaleDateString()}</p>
            </div>
          )}
        </div>
      ) : (
        <div className="rounded-lg border border-red-200 bg-red-50 p-6">
          <p className="text-red-700">This certificate could not be verified.</p>
        </div>
      )}
    </div>
  )
}
