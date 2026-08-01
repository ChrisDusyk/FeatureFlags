import { Link, useLocation } from 'react-router-dom';
import { PageHeader } from '../../shell/PageHeader';

export function NotFoundPage() {
  const { pathname } = useLocation();

  return (
    <>
      <PageHeader
        eyebrow="Console"
        title="No screen here"
        lede={`Nothing in the console answers to ${pathname}. Check the address, or start from the overview.`}
      />
      <p>
        <Link className="textlink" to="/">
          Go to the overview
        </Link>
      </p>
    </>
  );
}
