import axios from "axios";

const apiClient = axios.create({
  baseURL: "/api",
  headers: {
    "Content-Type": "application/json",
  },
  timeout: 15000,
});

apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem("qt_auth_token");
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem("qt_auth_token");
    }
    if (error.response?.status >= 500) {
      window.dispatchEvent(
        new CustomEvent("api-error", {
          detail: { message: "Server error. The backend may be restarting." },
        })
      );
    }
    return Promise.reject(error);
  }
);

export default apiClient;
