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
        width: '280px',
        height: '100vh',
        backgroundColor: 'var(--bg-sidebar)',
        borderRight: '1px solid var(--border)',
        display: 'flex',
        flexDirection: 'column' as const,
        position: 'sticky' as const,
        top: 0,
        zIndex: 100,
    },
    logo: {
        padding: '2.5rem 1.5rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    logoIcon: {
        fontSize: '1.75rem',
        filter: 'drop-shadow(0 0 8px var(--primary-glow))'
    },
    logoText: {
        fontSize: '1.5rem',
        fontWeight: '900',
        background: 'linear-gradient(135deg, #fff 0%, var(--primary) 100%)',
        WebkitBackgroundClip: 'text',
        WebkitTextFillColor: 'transparent',
        letterSpacing: '-0.03em',
    },
    nav: {
        flex: 1,
        padding: '0 1.25rem',
        overflowY: 'auto' as const,
    },
    group: { marginBottom: '2rem' },
    groupLabel: {
        fontSize: '0.65rem',
        textTransform: 'uppercase' as const,
        color: 'var(--text-muted)',
        letterSpacing: '0.15em',
        padding: '0 0.75rem 0.75rem',
        fontWeight: '800',
    },
    link: {
        display: 'flex',
        alignItems: 'center',
        padding: '0.85rem 1rem',
        color: 'var(--text-muted)',
        textDecoration: 'none',
        borderRadius: '12px',
        fontSize: '0.95rem',
        fontWeight: '600',
        transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
        marginBottom: '4px',
    },
    activeLink: {
        backgroundColor: 'rgba(56, 189, 248, 0.1)',
        color: 'var(--primary)',
        boxShadow: 'inset 0 0 0 1px rgba(56, 189, 248, 0.2)',
    },
    icon: {
        marginRight: '0.75rem',
        fontSize: '1.25rem',
    },
    footer: {
        padding: '1.5rem',
        borderTop: '1px solid var(--border)',
        background: 'rgba(0,0,0,0.2)'
    },
    userCard: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    avatar: {
        width: '40px',
        height: '40px',
        borderRadius: '12px',
        background: 'linear-gradient(135deg, var(--primary), var(--secondary))',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: '0.85rem',
        fontWeight: '800',
        color: 'white',
        boxShadow: '0 4px 12px rgba(0,0,0,0.3)'
    },
    userInfo: { overflow: 'hidden' },
    userName: { fontSize: '0.9rem', fontWeight: '700', color: '#fff', whiteSpace: 'nowrap' as const, overflow: 'hidden', textOverflow: 'ellipsis' },
    userRole: { fontSize: '0.75rem', color: 'var(--text-muted)', fontWeight: '500' },
};

export default Sidebar;