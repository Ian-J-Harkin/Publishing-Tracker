import { Link, useLocation } from 'react-router-dom';

const Sidebar = () => {
    const location = useLocation();

    const menuGroups = [
        {
            label: 'Main',
            items: [
                { path: '/', label: 'Dashboard', icon: '📊' },
            ]
        },
        {
            label: 'Inventory',
            items: [
                { path: '/books', label: 'Books', icon: '📚' },
                { path: '/platforms', label: 'Platforms', icon: '🌐' },
            ]
        },
        {
            label: 'Revenue',
            items: [
                { path: '/sales', label: 'Sales Ledger', icon: '💰' },
                { path: '/sales/add', label: 'Manual Entry', icon: '✍️' },
            ]
        },
        {
            label: 'Tools',
            items: [
                { path: '/import', label: 'Import Data', icon: '📥' },
                { path: '/import/history', label: 'Import History', icon: '🕒' },
            ]
        }
    ];

    const isActive = (path: string) => location.pathname === path;

    return (
        <aside style={sidebarStyles.container}>
            <div style={sidebarStyles.logo}>
                <span style={sidebarStyles.logoIcon}>📖</span>
                <span style={sidebarStyles.logoText}>PubTrack</span>
            </div>

            <nav style={sidebarStyles.nav}>
                {menuGroups.map((group) => (
                    <div key={group.label} style={sidebarStyles.group}>
                        <h4 style={sidebarStyles.groupLabel}>{group.label}</h4>
                        {group.items.map((item) => (
                            <Link
                                key={item.path}
                                to={item.path}
                                style={{
                                    ...sidebarStyles.link,
                                    ...(isActive(item.path) ? sidebarStyles.activeLink : {})
                                }}
                            >
                                <span style={sidebarStyles.icon}>{item.icon}</span>
                                {item.label}
                            </Link>
                        ))}
                    </div>
                ))}
            </nav>

            <div style={sidebarStyles.footer}>
                <div style={sidebarStyles.userCard}>
                    <div style={sidebarStyles.avatar}>AD</div>
                    <div style={sidebarStyles.userInfo}>
                        <div style={sidebarStyles.userName}>Admin User</div>
                        <div style={sidebarStyles.userRole}>Publisher</div>
                    </div>
                </div>
            </div>
        </aside>
    );
};

const sidebarStyles = {
    container: {
        width: '260px',
        height: '100vh',
        backgroundColor: '#ffffff',
        borderRight: '1px solid var(--border)',
        display: 'flex',
        flexDirection: 'column' as const,
        position: 'sticky' as const,
        top: 0,
    },
    logo: {
        padding: '2rem 1.5rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    logoIcon: { fontSize: '1.5rem' },
    logoText: {
        fontSize: '1.25rem',
        fontWeight: '800',
        color: 'var(--primary)',
        letterSpacing: '-0.02em',
    },
    nav: {
        flex: 1,
        padding: '0 1rem',
        overflowY: 'auto' as const,
    },
    group: { marginBottom: '1.5rem' },
    groupLabel: {
        fontSize: '0.7rem',
        textTransform: 'uppercase' as const,
        color: 'var(--text-muted)',
        letterSpacing: '0.05em',
        padding: '0 0.5rem 0.5rem',
        fontWeight: '700',
    },
    link: {
        display: 'flex',
        alignItems: 'center',
        padding: '0.75rem 1rem',
        color: 'var(--text-main)',
        textDecoration: 'none',
        borderRadius: '8px',
        fontSize: '0.9rem',
        fontWeight: '500',
        transition: 'all 0.2s ease',
        marginBottom: '2px',
    },
    activeLink: {
        backgroundColor: '#eff6ff', // Very light blue
        color: 'var(--primary)',
    },
    icon: {
        marginRight: '0.75rem',
        fontSize: '1.1rem',
    },
    footer: {
        padding: '1.5rem',
        borderTop: '1px solid var(--border)',
    },
    userCard: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    avatar: {
        width: '36px',
        height: '36px',
        borderRadius: '50%',
        backgroundColor: '#e2e8f0',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: '0.8rem',
        fontWeight: '600',
    },
    userInfo: { overflow: 'hidden' },
    userName: { fontSize: '0.85rem', fontWeight: '600', whiteSpace: 'nowrap' as const, overflow: 'hidden', textOverflow: 'ellipsis' },
    userRole: { fontSize: '0.75rem', color: 'var(--text-muted)' },
};

export default Sidebar;