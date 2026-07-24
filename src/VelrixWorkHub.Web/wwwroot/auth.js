window.adminAuth = {
    login: async (username, password, remember) => {
        const response = await fetch('/api/admin/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ username, password, remember })
        });

        const body = await response.json().catch(() => ({ ok: false, error: '登录服务返回了无效响应。' }));
        return { ok: response.ok && body.ok === true, error: body.error || null };
    },

    getProfile: async () => {
        const response = await fetch('/api/admin/profile', {
            method: 'GET',
            credentials: 'same-origin'
        });
        return await readApiResponse(response);
    },

    updateProfile: async (nickname) => {
        const response = await fetch('/api/admin/profile', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ nickname })
        });
        return await readApiResponse(response);
    },

    changePassword: async (oldPassword, newPassword, confirmPassword) => {
        const response = await fetch('/api/admin/profile/password', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ oldPassword, newPassword, confirmPassword })
        });
        return await readApiResponse(response);
    },

    logoutAll: async () => {
        const response = await fetch('/api/admin/logout-all', {
            method: 'POST',
            credentials: 'same-origin'
        });
        return await readApiResponse(response);
    }
};

async function readApiResponse(response) {
    const body = await response.json().catch(() => ({ ok: false, error: '管理接口返回了无效响应。' }));
    return {
        ok: response.ok && body.ok === true,
        error: body.error || null,
        message: body.message || null,
        data: body.data || null
    };
}
