import { useEffect, useState } from 'react';
import { User } from '../adapters/UserAxiosAdapter';
import { useDependencies } from '../context/DependencyContext';

const AdminUsersPage = () => {
    const { users: userServicePort } = useDependencies();
    const [users, setUsers] = useState<User[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchUsers = async () => {
        try {
            setLoading(true);
            const data = await userServicePort.getUsers();
            setUsers(data);
        } catch {
            setError('Failed to fetch users.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchUsers();
    }, []);

    const handlePromote = async (id: number) => {
        try {
            await userServicePort.promoteUser(id);
            await fetchUsers();
        } catch (err: any) {
            setError(err.response?.data?.error || 'Failed to promote user.');
        }
    };

    const handleDemote = async (id: number) => {
        if (!window.confirm("Are you sure you want to revoke Admin rights from this user?")) return;
        try {
            await userServicePort.demoteUser(id);
            await fetchUsers();
        } catch (err: any) {
            setError(err.response?.data || 'Failed to demote user.');
        }
    };

    if (loading) return <div style={pageStyles.centered}>Loading users...</div>;

    return (
        <div style={pageStyles.container}>
            <div style={pageStyles.header}>
                <div>
                    <h1 style={{ color: '#1e293b' }}>Manage Users</h1>
                    <p style={pageStyles.subtitle}>Delegate or revoke administrator permissions.</p>
                </div>
            </div>

            {error && <div style={pageStyles.errorBanner}>{error}</div>}

            <div className="card" style={{ padding: '0', overflow: 'hidden' }}>
                <table style={pageStyles.table}>
                    <thead>
                        <tr style={pageStyles.thRow}>
                            <th style={pageStyles.th}>ID</th>
                            <th style={pageStyles.th}>Name</th>
                            <th style={pageStyles.th}>Email</th>
                            <th style={pageStyles.th}>Role</th>
                            <th style={pageStyles.th}>Registered</th>
                            <th style={pageStyles.th}>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {users.length === 0 ? (
                            <tr>
                                <td colSpan={6} style={{ textAlign: 'center', padding: '2rem' }}>No users found.</td>
                            </tr>
                        ) : (
                            users.map(u => (
                                <tr key={u.id} style={pageStyles.tr}>
                                    <td style={pageStyles.td}>{u.id}</td>
                                    <td style={{ ...pageStyles.td, fontWeight: 'bold' }}>{u.lastName}, {u.firstName}</td>
                                    <td style={pageStyles.td}>{u.email}</td>
                                    <td style={pageStyles.td}>
                                        <span style={u.role === 'Admin' ? pageStyles.badgeAdmin : pageStyles.badgeUser}>
                                            {u.role}
                                        </span>
                                    </td>
                                    <td style={pageStyles.td}>{new Date(u.createdAt).toLocaleDateString()}</td>
                                    <td style={pageStyles.td}>
                                        {u.role === 'Admin' ? (
                                            <button 
                                                className="btn-danger" 
                                                style={{ padding: '0.4rem 0.8rem', fontSize: '0.85rem' }}
                                                onClick={() => handleDemote(u.id)}
                                            >
                                                Revoke Admin
                                            </button>
                                        ) : (
                                            <button 
                                                className="btn-primary" 
                                                style={{ padding: '0.4rem 0.8rem', fontSize: '0.85rem' }}
                                                onClick={() => handlePromote(u.id)}
                                            >
                                                Promote to Admin
                                            </button>
                                        )}
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

const pageStyles = {
    container: { animation: 'fadeIn 0.5s ease-out' },
    header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '3rem' },
    subtitle: { color: 'var(--text-muted)', fontSize: '1.1rem' },
    errorBanner: { padding: '1.5rem', backgroundColor: 'rgba(239, 68, 68, 0.1)', color: 'var(--danger)', borderRadius: '16px', border: '1px solid var(--danger)', marginBottom: '2rem' },
    centered: { padding: '10rem 2rem', textAlign: 'center' as const, fontSize: '1.2rem', color: 'var(--text-muted)' },
    table: { width: '100%', borderCollapse: 'collapse' as const, textAlign: 'left' as const },
    thRow: { backgroundColor: 'rgba(0,0,0,0.02)' },
    th: { padding: '1rem 1.5rem', color: 'var(--text-muted)', fontSize: '0.85rem', textTransform: 'uppercase' as const, letterSpacing: '0.05em', borderBottom: '1px solid var(--border)' },
    tr: { borderBottom: '1px solid var(--border)', transition: 'background-color 0.2s' },
    td: { padding: '1rem 1.5rem', verticalAlign: 'middle' },
    badgeAdmin: { backgroundColor: 'rgba(99, 102, 241, 0.1)', color: '#4f46e5', padding: '4px 12px', borderRadius: '20px', fontWeight: '800', fontSize: '0.75rem', textTransform: 'uppercase' as const },
    badgeUser: { backgroundColor: 'rgba(148, 163, 184, 0.1)', color: '#64748b', padding: '4px 12px', borderRadius: '20px', fontWeight: '800', fontSize: '0.75rem', textTransform: 'uppercase' as const },
};

export default AdminUsersPage;
