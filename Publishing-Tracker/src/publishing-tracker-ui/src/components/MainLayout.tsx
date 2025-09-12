import { Outlet, Link } from 'react-router-dom';
import ProtectedRoute from './ProtectedRoute';

const MainLayout = () => {
    return (
        <ProtectedRoute>
            <div className="main-layout">
                <nav>
                    <ul>
                        <li><Link to="/dashboard">Dashboard</Link></li>
                        <li><Link to="/books">Books</Link></li>
                        <li><Link to="/sales">Sales</Link></li>
                        <li><Link to="/import">Import</Link></li>
                        <li><Link to="/platforms">Platforms</Link></li>
                    </ul>
                </nav>
                <main>
                    <Outlet />
                </main>
            </div>
        </ProtectedRoute>
    );
};

export default MainLayout;