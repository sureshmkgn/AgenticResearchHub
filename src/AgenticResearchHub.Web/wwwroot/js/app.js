document.addEventListener('DOMContentLoaded', () => {
    // -------------------------------------------------------------
    // Elements & State
    // -------------------------------------------------------------
    let currentReport = null;
    let isResearching = false;

    // Tabs
    const tabButtons = document.querySelectorAll('.tab-btn');
    const tabPanels = document.querySelectorAll('.tab-panel');

    // Research Form
    const researchForm = document.getElementById('research-form');
    const topicInput = document.getElementById('topic-input');
    const contextInput = document.getElementById('context-input');
    const searchEngineSelect = document.getElementById('search-engine-select');
    const guardrailsToggle = document.getElementById('guardrails-toggle');
    const btnStartResearch = document.getElementById('btn-start-research');
    const btnClearStream = document.getElementById('btn-clear-stream');
    const activityFeed = document.getElementById('activity-feed');
    const meshStatusIndicator = document.getElementById('mesh-status-indicator');

    // Report Tab
    const reportBadge = document.getElementById('report-badge');
    const reportContent = document.getElementById('report-content');
    const reportTopicBadge = document.getElementById('report-topic-badge');
    const reportDurationBadge = document.getElementById('report-duration-badge');
    const keyFindingsList = document.getElementById('key-findings-list');
    const citationsList = document.getElementById('citations-list');
    const citationsCount = document.getElementById('citations-count');
    const btnCopyMarkdown = document.getElementById('btn-copy-markdown');
    const btnPrintReport = document.getElementById('btn-print-report');

    // Semantic Vector Search Tab
    const semanticSearchInput = document.getElementById('semantic-search-input');
    const btnSemanticSearch = document.getElementById('btn-semantic-search');
    const similaritySlider = document.getElementById('similarity-slider');
    const similarityValue = document.getElementById('similarity-value');
    const semanticResults = document.getElementById('semantic-results');
    const storedReportsCount = document.getElementById('stored-reports-count');

    // A2A & Telemetry Tab
    const a2aMessageFeed = document.getElementById('a2a-message-feed');
    const btnRefreshA2A = document.getElementById('btn-refresh-a2a');
    const statReports = document.getElementById('stat-reports');
    const statA2ACount = document.getElementById('stat-a2a-count');

    // -------------------------------------------------------------
    // Tab Navigation Logic
    // -------------------------------------------------------------
    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const target = btn.getAttribute('data-tab');
            tabButtons.forEach(b => b.classList.remove('active'));
            tabPanels.forEach(p => p.classList.remove('active'));

            btn.classList.add('active');
            document.getElementById(target)?.classList.add('active');

            if (target === 'tab-a2a') {
                loadA2AMessages();
                loadSystemInfo();
            } else if (target === 'tab-vector') {
                loadAllStoredReports();
            }
        });
    });

    // Quick prompt chips
    document.querySelectorAll('.chip').forEach(chip => {
        chip.addEventListener('click', () => {
            topicInput.value = chip.getAttribute('data-topic');
            topicInput.focus();
        });
    });

    // -------------------------------------------------------------
    // Research Execution (Server-Sent Events)
    // -------------------------------------------------------------
    researchForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        if (isResearching) return;

        const topic = topicInput.value.trim();
        if (!topic) return;

        isResearching = true;
        btnStartResearch.disabled = true;
        btnStartResearch.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Researching in Progress...`;
        meshStatusIndicator.textContent = "Executing Multi-Agent Workflow";
        meshStatusIndicator.style.color = "var(--accent-secondary)";

        // Clear previous stream & reset topology nodes
        activityFeed.innerHTML = '';
        resetAgentTopology();

        const requestPayload = {
            topic: topic,
            userContext: contextInput.value.trim() || null,
            searchEngine: searchEngineSelect.value,
            enableGuardrails: guardrailsToggle.checked,
            maxIterations: 3
        };

        try {
            const response = await fetch('/api/research/stream', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(requestPayload)
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const reader = response.body.getReader();
            const decoder = new TextDecoder('utf-8');
            let buffer = '';

            while (true) {
                const { done, value } = await reader.read();
                if (done) break;

                buffer += decoder.decode(value, { stream: true });
                const lines = buffer.split('\n');
                buffer = lines.pop() || '';

                for (const line of lines) {
                    if (line.startsWith('data: ')) {
                        const dataStr = line.replace('data: ', '').trim();
                        if (dataStr === '[DONE]') {
                            showToast("Multi-Agent Research Mission Completed!");
                            meshStatusIndicator.textContent = "Completed";
                            meshStatusIndicator.style.color = "var(--accent-success)";
                            continue;
                        }

                        try {
                            const step = JSON.parse(dataStr);
                            handleAgentStepEvent(step);
                        } catch (err) {
                            console.error("Error parsing SSE JSON event:", err, dataStr);
                        }
                    }
                }
            }
        } catch (err) {
            console.error("Workflow failed:", err);
            appendStepToFeed({
                agent: "System",
                title: "Workflow Execution Error",
                details: err.message,
                status: "Failed",
                timestampUtc: new Date().toISOString()
            });
            showToast("Research workflow failed: " + err.message);
        } finally {
            isResearching = false;
            btnStartResearch.disabled = false;
            btnStartResearch.innerHTML = `<i class="fa-solid fa-rocket"></i> Launch Multi-Agent Workflow`;
            loadSystemInfo();
        }
    });

    function handleAgentStepEvent(step) {
        appendStepToFeed(step);
        updateAgentNodeState(step.agent, step.status);

        // Check if final report payload arrived
        if (step.payload && step.payload.fullContentMarkdown) {
            currentReport = step.payload;
            renderReport(currentReport);
        }
    }

    function appendStepToFeed(step) {
        const card = document.createElement('div');
        card.className = `step-card step-${step.status}`;

        const time = new Date(step.timestampUtc).toLocaleTimeString();
        card.innerHTML = `
            <div class="step-top">
                <span class="step-agent-tag">[${step.agent}]</span>
                <span class="step-time">${time}</span>
            </div>
            <div class="step-title">${escapeHtml(step.title)}</div>
            <div class="step-details">${escapeHtml(step.details)}</div>
        `;
        activityFeed.appendChild(card);
        activityFeed.scrollTop = activityFeed.scrollHeight;
    }

    function updateAgentNodeState(agentRole, status) {
        const node = document.querySelector(`.agent-node[data-role="${agentRole}"]`);
        if (!node) return;

        const statusEl = node.querySelector('.node-status');

        if (status === 'InProgress' || status === 1) {
            node.classList.remove('done');
            node.classList.add('active');
            if (statusEl) statusEl.textContent = 'Active...';
        } else if (status === 'Completed' || status === 2) {
            node.classList.remove('active');
            node.classList.add('done');
            if (statusEl) statusEl.textContent = 'Completed';
        } else if (status === 'Failed' || status === 3) {
            node.classList.remove('active');
            if (statusEl) statusEl.textContent = 'Failed';
        }
    }

    function resetAgentTopology() {
        document.querySelectorAll('.agent-node').forEach(node => {
            node.classList.remove('active', 'done');
            const statusEl = node.querySelector('.node-status');
            if (statusEl) statusEl.textContent = 'Standby';
        });
    }

    btnClearStream.addEventListener('click', () => {
        activityFeed.innerHTML = `
            <div class="feed-placeholder">
                <i class="fa-solid fa-circle-nodes"></i>
                <p>Launch a research mission to monitor live agent reasoning, tool invocations, and handoffs.</p>
            </div>`;
    });

    // -------------------------------------------------------------
    // Report Rendering Logic
    // -------------------------------------------------------------
    function renderReport(report) {
        reportBadge.style.display = 'inline-block';
        reportTopicBadge.textContent = report.topic;
        
        const seconds = report.totalExecutionDuration ? 
            (typeof report.totalExecutionDuration === 'string' ? report.totalExecutionDuration : report.totalExecutionDuration) : '0s';
        reportDurationBadge.innerHTML = `<i class="fa-solid fa-clock"></i> Generated in ${seconds}`;

        // Render Markdown safely
        if (window.marked) {
            reportContent.innerHTML = marked.parse(report.fullContentMarkdown || '');
        } else {
            reportContent.innerHTML = `<pre>${escapeHtml(report.fullContentMarkdown)}</pre>`;
        }

        // Populate Key Findings
        keyFindingsList.innerHTML = '';
        if (report.keyFindings && report.keyFindings.length > 0) {
            report.keyFindings.forEach(f => {
                const li = document.createElement('li');
                li.textContent = f;
                keyFindingsList.appendChild(li);
            });
        } else {
            keyFindingsList.innerHTML = `<li class="text-muted">No explicit bullet findings.</li>`;
        }

        // Populate Citations
        citationsList.innerHTML = '';
        citationsCount.textContent = report.citations ? report.citations.length : 0;

        if (report.citations && report.citations.length > 0) {
            report.citations.forEach(c => {
                const item = document.createElement('div');
                item.className = 'citation-item';
                item.innerHTML = `
                    <a href="${escapeHtml(c.url)}" target="_blank" rel="noopener noreferrer" class="citation-link">
                        [${c.index}] ${escapeHtml(c.title)}
                    </a>
                    <p style="color: var(--text-secondary); margin-bottom: 4px;">${escapeHtml(c.snippet)}</p>
                    <div class="citation-meta">
                        <span><i class="fa-solid fa-link"></i> ${escapeHtml(c.sourceDomain || 'Web')}</span> | 
                        <span>Relevance: ${(c.relevanceScore * 100).toFixed(0)}%</span>
                    </div>
                `;
                citationsList.appendChild(item);
            });
        } else {
            citationsList.innerHTML = `<p class="text-muted">No external citations found.</p>`;
        }
    }

    btnCopyMarkdown.addEventListener('click', () => {
        if (!currentReport || !currentReport.fullContentMarkdown) {
            showToast("No report content available to copy.");
            return;
        }
        navigator.clipboard.writeText(currentReport.fullContentMarkdown)
            .then(() => showToast("Markdown report copied to clipboard!"))
            .catch(err => console.error("Clipboard copy failed:", err));
    });

    btnPrintReport.addEventListener('click', () => {
        window.print();
    });

    // -------------------------------------------------------------
    // Semantic Vector Search Logic
    // -------------------------------------------------------------
    similaritySlider.addEventListener('input', () => {
        similarityValue.textContent = parseFloat(similaritySlider.value).toFixed(2);
    });

    btnSemanticSearch.addEventListener('click', executeSemanticSearch);
    semanticSearchInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') executeSemanticSearch();
    });

    async function executeSemanticSearch() {
        const query = semanticSearchInput.value.trim();
        if (!query) {
            loadAllStoredReports();
            return;
        }

        semanticResults.innerHTML = '<div class="text-muted" style="grid-column: 1/-1; text-align: center;"><i class="fa-solid fa-spinner fa-spin"></i> Executing vector cosine similarity ranking...</div>';

        try {
            const response = await fetch('/api/reports/search', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    query: query,
                    limit: 10,
                    minSimilarity: parseFloat(similaritySlider.value)
                })
            });

            if (!response.ok) throw new Error('Failed to perform semantic search');
            const data = await response.json();
            renderSemanticResults(data);
        } catch (err) {
            semanticResults.innerHTML = `<div class="text-danger" style="grid-column: 1/-1;">Error: ${err.message}</div>`;
        }
    }

    async function loadAllStoredReports() {
        try {
            const response = await fetch('/api/reports?limit=20');
            if (!response.ok) return;
            const reports = await response.json();
            storedReportsCount.textContent = reports.length;
            statReports.textContent = reports.length;

            if (reports.length === 0) {
                semanticResults.innerHTML = `
                    <div class="feed-placeholder" style="grid-column: 1/-1; padding: 40px;">
                        <i class="fa-solid fa-database"></i>
                        <p>No reports currently indexed. Complete your first research mission to build the knowledge base.</p>
                    </div>`;
                return;
            }

            semanticResults.innerHTML = '';
            reports.forEach(r => {
                const card = document.createElement('div');
                card.className = 'vector-card';
                card.innerHTML = `
                    <div>
                        <div class="similarity-badge"><i class="fa-solid fa-clock"></i> ${new Date(r.createdAtUtc).toLocaleDateString()}</div>
                        <h4 class="vector-title">${escapeHtml(r.title || r.topic)}</h4>
                        <p class="vector-snippet">${escapeHtml(r.executiveSummary || r.topic)}</p>
                    </div>
                    <button class="btn-secondary btn-view-report" style="align-self: flex-start; margin-top: 10px;">
                        <i class="fa-solid fa-eye"></i> View Full Report
                    </button>
                `;

                card.querySelector('.btn-view-report').addEventListener('click', () => {
                    currentReport = r;
                    renderReport(r);
                    document.getElementById('tab-btn-report').click();
                });

                semanticResults.appendChild(card);
            });
        } catch (err) {
            console.error("Failed to load reports:", err);
        }
    }

    function renderSemanticResults(results) {
        if (!results || results.length === 0) {
            semanticResults.innerHTML = `
                <div class="feed-placeholder" style="grid-column: 1/-1; padding: 40px;">
                    <i class="fa-solid fa-magnifying-glass"></i>
                    <p>No vector matches found above cosine similarity threshold.</p>
                </div>`;
            return;
        }

        semanticResults.innerHTML = '';
        results.forEach(res => {
            const r = res.report;
            const scorePercent = (res.similarityScore * 100).toFixed(1);
            const card = document.createElement('div');
            card.className = 'vector-card';
            card.innerHTML = `
                <div>
                    <div class="similarity-badge" style="background: rgba(6, 182, 212, 0.2); color: #06b6d4;">
                        <i class="fa-solid fa-bolt"></i> ${scorePercent}% Semantic Match
                    </div>
                    <h4 class="vector-title">${escapeHtml(r.title || r.topic)}</h4>
                    <p class="vector-snippet">${escapeHtml(res.matchedSnippet)}</p>
                </div>
                <button class="btn-secondary btn-view-report" style="align-self: flex-start; margin-top: 10px;">
                    <i class="fa-solid fa-eye"></i> View Full Report
                </button>
            `;

            card.querySelector('.btn-view-report').addEventListener('click', () => {
                currentReport = r;
                renderReport(r);
                document.getElementById('tab-btn-report').click();
            });

            semanticResults.appendChild(card);
        });
    }

    // -------------------------------------------------------------
    // A2A Mesh & Telemetry Logic
    // -------------------------------------------------------------
    btnRefreshA2A?.addEventListener('click', loadA2AMessages);

    async function loadA2AMessages() {
        try {
            const res = await fetch('/api/a2a/history');
            if (!res.ok) return;
            const messages = await res.json();
            statA2ACount.textContent = messages.length;

            if (messages.length === 0) {
                a2aMessageFeed.innerHTML = '<p class="text-muted">No A2A messages published yet.</p>';
                return;
            }

            a2aMessageFeed.innerHTML = '';
            messages.forEach(m => {
                const card = document.createElement('div');
                card.className = 'a2a-card';
                card.innerHTML = `
                    <div class="a2a-header">
                        <span class="a2a-route">${escapeHtml(m.sender)} &rarr; ${escapeHtml(m.recipient || 'All')}</span>
                        <span class="text-muted">${new Date(m.timestampUtc).toLocaleTimeString()}</span>
                    </div>
                    <div class="a2a-body">${escapeHtml(m.content)}</div>
                `;
                a2aMessageFeed.appendChild(card);
            });
            a2aMessageFeed.scrollTop = a2aMessageFeed.scrollHeight;
        } catch (err) {
            console.error("Failed to load A2A messages:", err);
        }
    }

    async function loadSystemInfo() {
        try {
            const res = await fetch('/api/system/info');
            if (!res.ok) return;
            const info = await res.json();

            document.getElementById('nav-llm-label').textContent = `${info.llmProvider} ${info.llmModel}`;
            document.getElementById('nav-search-label').textContent = `${info.searchEngine} Web Search`;
            storedReportsCount.textContent = info.storedReportsCount;
            statReports.textContent = info.storedReportsCount;
        } catch (err) {
            console.error("Failed to load system info:", err);
        }
    }

    // -------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------
    function showToast(msg) {
        const toast = document.getElementById('toast');
        toast.textContent = msg;
        toast.classList.add('show');
        setTimeout(() => toast.classList.remove('show'), 3500);
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    // Initial load
    loadSystemInfo();
    loadAllStoredReports();
});
