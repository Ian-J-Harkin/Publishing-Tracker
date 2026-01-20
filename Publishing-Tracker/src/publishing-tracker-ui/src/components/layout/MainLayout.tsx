import { Outlet, Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import ProtectedRoute from '../ProtectedRoute';

const MainLayout = () => {
    const { logout } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();

    const handleLogout = () => {
        logout();
        navigate('/login');
    };

    const isActive = (path: string) => location.pathname === path;

    return (
        <ProtectedRoute>
            <div style={layoutStyles.container}>
                {/* Fixed Sidebar */}
                <aside style={layoutStyles.sidebar}>
                    <div>
                        <div style={layoutStyles.logo}>PUBLISH PRO</div>
                        
                        <nav style={layoutStyles.nav}>
                            <h4 style={layoutStyles.navGroupLabel}>Main</h4>
                            <Link to="/dashboard" style={{...layoutStyles.navLink, ...(isActive('/dashboard') ? layoutStyles.active : {})}}>📊 Dashboard</Link>
                            
                            <h4 style={layoutStyles.navGroupLabel}>Inventory</h4>
                            <Link to="/books" style={{...layoutStyles.navLink, ...(isActive('/books') ? layoutStyles.active : {})}}>📚 Books</Link>
                            <Link to="/platforms" style={{...layoutStyles.navLink, ...(isActive('/platforms') ? layoutStyles.active : {})}}>🌐 Platforms</Link>
                            
                            <h4 style={layoutStyles.navGroupLabel}>Revenue</h4>
                            <Link to="/sales" style={{...layoutStyles.navLink, ...(isActive('/sales') ? layoutStyles.active : {})}}>💰 Sales Ledger</Link>
                            <Link to="/sales/add" style={{...layoutStyles.navLink, ...(isActive('/sales/add') ? layoutStyles.active : {})}}>✍️ Manual Entry</Link>
                            
                            <h4 style={layoutStyles.navGroupLabel}>Tools</h4>
                            <Link to="/import" style={{...layoutStyles.navLink, ...(isActive('/import') ? layoutStyles.active : {})}}>📥 Import Data</Link>
                            <Link to="/import/history" style={{...layoutStyles.navLink, ...(isActive('/import/history') ? layoutStyles.active : {})}}>🕒 Import History</Link>
                        </nav>
                    </div>
                    
                    <button onClick={handleLogout} style={layoutStyles.logoutBtn}>
                        🚪 Logout
                    </button>
                </aside>

                {/* Main Content Area */}
                <div style={layoutStyles.main}>
                    <header style={layoutStyles.topBar}>
                        <div style={layoutStyles.breadcrumb}>
                            App / <span style={{ color: 'var(--primary)', fontWeight: '600' }}>
                                {location.pathname.substring(1).replace('/', ' / ') || 'Dashboard'}
                            </span>
                        </div>
                        <div style={layoutStyles.userCircle}>JD</div>
                    </header>
                    
                    <main style={layoutStyles.content}>
                        <div style={layoutStyles.innerContent}>
                            <Outlet />
                        </div>
                    </main>
                </div>
            </div>
        </ProtectedRoute>
    );
};

const layoutStyles = {
    container: { display: 'flex', height: '100vh', width: '100vw', backgroundColor: '#f8fafc', overflow: 'hidden' },
    sidebar: { 
        width: '260px', 
        backgroundColor: '#0f172a', 
        padding: '2rem 1.25rem', 
        display: 'flex', 
        flexDirection: 'column' as const, 
        justifyContent: 'space-between',
        flexShrink: 0
    },
    logo: { color: '#38bdf8', fontSize: '1.25rem', fontWeight: '800', marginBottom: '2.5rem', textAlign: 'center' as const, letterSpacing: '1px' },
    nav: { display: 'flex', flexDirection: 'column' as const, gap: '4px' },
    navGroupLabel: { color: '#475569', fontSize: '0.65rem', textTransform: 'uppercase' as const, letterSpacing: '1px', marginTop: '1.5rem', marginBottom: '0.5rem', paddingLeft: '1rem' },
    navLink: { color: '#94a3b8', textDecoration: 'none', padding: '0.7rem 1rem', borderRadius: '8px', transition: '0.2s', fontSize: '0.9rem', display: 'flex', alignItems: 'center', gap: '10px' },
    active: { backgroundColor: '#1e293b', color: '#38bdf8', fontWeight: '600' },
    logoutBtn: { backgroundColor: 'transparent', border: '1px solid #334155', color: '#94a3b8', padding: '0.75rem', borderRadius: '8px', cursor: 'pointer', transition: '0.2s' },
    main: { flex: 1, display: 'flex', flexDirection: 'column' as const, overflow: 'hidden' },
    topBar: { height: '64px', backgroundColor: 'white', borderBottom: '1px solid #e2e8f0', display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 2.5rem', flexShrink: 0 },
    breadcrumb: { color: '#64748b', fontSize: '0.8rem', textTransform: 'uppercase' as const, letterSpacing: '0.5px' },
    userCircle: { width: '35px', height: '35px', borderRadius: '50%', backgroundColor: '#38bdf8', display: 'grid', placeItems: 'center', color: 'white', fontWeight: 'bold' as const },
    content: { flex: 1, overflowY: 'auto' as const, backgroundColor: '#f8fafc' },
    innerContent: { maxWidth: '1400px', margin: '0 auto', padding: '2.5rem' }
};

export default MainLayout;