import React, { useState, useEffect } from 'react';
import { User } from '../adapters/UserAxiosAdapter';
import { useDependencies } from '../context/DependencyContext';

const ProfilePage: React.FC = () => {
    const { users } = useDependencies();
    const [profile, setProfile] = useState<User | null>(null);
    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');
    const [oldPassword, setOldPassword] = useState('');
    const [newPassword, setNewPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');

    const [profileMessage, setProfileMessage] = useState({ text: '', type: '' });
    const [passwordMessage, setPasswordMessage] = useState({ text: '', type: '' });
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        loadProfile();
    }, []);

    const loadProfile = async () => {
        try {
            const data = await users.getProfile();
            setProfile(data);
            setFirstName(data.firstName);
            setLastName(data.lastName);
        } catch (error) {
            console.error("Failed to load profile", error);
        } finally {
            setIsLoading(false);
        }
    };

    const handleUpdateProfile = async (e: React.FormEvent) => {
        e.preventDefault();
        setProfileMessage({ text: '', type: '' });
        try {
            await users.updateProfile(firstName, lastName);
            setProfileMessage({ text: 'Profile updated successfully!', type: 'success' });
            if (profile) {
                setProfile({ ...profile, firstName, lastName });
            }
        } catch (error: any) {
            setProfileMessage({ text: error.response?.data || 'Failed to update profile', type: 'error' });
        }
    };

    const handleChangePassword = async (e: React.FormEvent) => {
        e.preventDefault();
        setPasswordMessage({ text: '', type: '' });

        if (newPassword !== confirmPassword) {
            setPasswordMessage({ text: 'New passwords do not match', type: 'error' });
            return;
        }

        try {
            await users.changePassword(oldPassword, newPassword);
            setPasswordMessage({ text: 'Password changed successfully!', type: 'success' });
            setOldPassword('');
            setNewPassword('');
            setConfirmPassword('');
        } catch (error: any) {
            setPasswordMessage({ text: error.response?.data || 'Failed to change password', type: 'error' });
        }
    };

    if (isLoading) {
        return <div className="loading-spinner">Loading Profile...</div>;
    }

    return (
        <div className="page-container">
            <h1 className="page-title">My Profile</h1>
            <p className="page-subtitle">Manage your personal information and security settings.</p>

            <div className="form-card" style={{ marginBottom: '2rem' }}>
                <h2>Profile Information</h2>
                <p><strong>Email:</strong> {profile?.email}</p>
                <p><strong>Role:</strong> {profile?.role}</p>

                {profileMessage.text && (
                    <div className={`alert alert-${profileMessage.type}`}>
                        {profileMessage.text}
                    </div>
                )}

                <form onSubmit={handleUpdateProfile} style={{ marginTop: '1rem' }}>
                    <div className="form-group">
                        <label>First Name</label>
                        <input
                            type="text"
                            value={firstName}
                            onChange={(e) => setFirstName(e.target.value)}
                            required
                        />
                    </div>
                    <div className="form-group">
                        <label>Last Name</label>
                        <input
                            type="text"
                            value={lastName}
                            onChange={(e) => setLastName(e.target.value)}
                            required
                        />
                    </div>
                    <button type="submit" className="btn btn-primary">Update Profile</button>
                </form>
            </div>

            <div className="form-card">
                <h2>Change Password</h2>
                {passwordMessage.text && (
                    <div className={`alert alert-${passwordMessage.type}`}>
                        {passwordMessage.text}
                    </div>
                )}
                <form onSubmit={handleChangePassword} style={{ marginTop: '1rem' }}>
                    <div className="form-group">
                        <label>Current Password</label>
                        <input
                            type="password"
                            value={oldPassword}
                            onChange={(e) => setOldPassword(e.target.value)}
                            required
                        />
                    </div>
                    <div className="form-group">
                        <label>New Password</label>
                        <input
                            type="password"
                            value={newPassword}
                            onChange={(e) => setNewPassword(e.target.value)}
                            required
                        />
                    </div>
                    <div className="form-group">
                        <label>Confirm New Password</label>
                        <input
                            type="password"
                            value={confirmPassword}
                            onChange={(e) => setConfirmPassword(e.target.value)}
                            required
                        />
                    </div>
                    <button type="submit" className="btn btn-secondary">Change Password</button>
                </form>
            </div>
        </div>
    );
};

export default ProfilePage;
