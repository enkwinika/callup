$filePath = "Views\Dashboard\Provider.cshtml"
$lines = [System.IO.File]::ReadAllLines((Resolve-Path $filePath))

# Find start and end of the view-settings section
$startLine = -1
$endLine = -1
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'class="view-section" id="view-settings"') { $startLine = $i }
    if ($startLine -ge 0 -and $i -gt $startLine -and $lines[$i] -match '^</div>$') { $endLine = $i; break }
}

Write-Host "Start: $startLine, End: $endLine"

$newSection = @'
<div class="view-section" id="view-settings">

    <div class="profile-hero fade-up">
        <div class="profile-avatar-lg" style="background: var(--secondary);"><i class="fa-solid fa-briefcase" style="font-size:1.5rem;"></i></div>
        <div class="profile-hero-info">
            <h2 class="profile-hero-name">Business Settings</h2>
            <p class="profile-hero-email">Manage your banking details and service preferences</p>
        </div>
    </div>

    <form id="businessSettingsForm">
        @Html.AntiForgeryToken()
        <div class="profile-grid fade-up" style="animation-delay:0.1s;">

            <div class="card-section">
                <div class="table-header">
                    <div><h3>Payout Details</h3><p>Banking information for receiving payments</p></div>
                    <i class="fa-solid fa-building-columns" style="color:var(--secondary);font-size:1.2rem;opacity:0.4;"></i>
                </div>
                <div style="padding:28px;">
                    <div class="form-group">
                        <label>Bank Name</label>
                        <select name="bankName">
                            <option value="">Select your bank</option>
                            <option>FNB / First National Bank</option>
                            <option>Standard Bank</option>
                            <option>ABSA</option>
                            <option>Nedbank</option>
                            <option>Capitec</option>
                            <option>TymeBank</option>
                        </select>
                    </div>
                    <div style="display:grid;grid-template-columns:1fr 1fr;gap:16px;">
                        <div class="form-group">
                            <label>Account Number</label>
                            <input type="text" name="accountNumber" placeholder="e.g. 62012345678">
                        </div>
                        <div class="form-group">
                            <label>Branch Code</label>
                            <input type="text" name="branchCode" placeholder="e.g. 250655">
                        </div>
                    </div>
                    <div class="form-group">
                        <label>Account Type</label>
                        <select name="accountType">
                            <option>Cheque / Current Account</option>
                            <option>Savings Account</option>
                        </select>
                    </div>
                </div>
            </div>

            <div class="card-section">
                <div class="table-header">
                    <div><h3>Service Configuration</h3><p>Control how you receive job leads</p></div>
                    <i class="fa-solid fa-sliders" style="color:var(--primary);font-size:1.2rem;opacity:0.4;"></i>
                </div>
                <div style="padding:28px;">
                    <div class="form-group">
                        <label>Service Radius <span id="radiusVal" style="color:var(--primary);font-weight:800;">15</span> km</label>
                        <input type="range" min="1" max="100" value="15" name="serviceRadius"
                               oninput="document.getElementById('radiusVal').textContent=this.value"
                               style="width:100%;accent-color:var(--primary);margin-top:4px;">
                    </div>
                    <div class="form-group">
                        <label>Availability Status</label>
                        <select name="availability">
                            <option>Available - Accepting new jobs</option>
                            <option>Busy - Temporarily unavailable</option>
                            <option>On Leave</option>
                        </select>
                    </div>
                </div>
            </div>

        </div>

        <div class="card-section fade-up" style="animation-delay:0.2s;">
            <div style="padding:24px 28px;display:flex;align-items:flex-start;gap:20px;">
                <div style="flex:1;">
                    <div style="display:flex;align-items:center;gap:10px;margin-bottom:6px;">
                        <i class="fa-solid fa-bolt" style="color:var(--accent);font-size:1.1rem;"></i>
                        <span style="font-weight:800;font-size:1rem;">Instant Payouts</span>
                        <span style="font-size:0.7rem;font-weight:700;background:var(--accent-light);color:var(--accent);padding:2px 8px;border-radius:99px;text-transform:uppercase;">2.5% fee</span>
                    </div>
                    <p style="font-size:0.88rem;color:var(--text-muted);line-height:1.6;">
                        Funds released immediately after admin approval instead of the standard 48-hour window.
                    </p>
                </div>
                <label class="toggle-switch" style="flex-shrink:0;margin-top:4px;">
                    <input type="checkbox" name="isInstantPayment" value="true">
                    <span class="toggle-slider"></span>
                </label>
            </div>
        </div>

        <div style="display:flex;justify-content:flex-end;gap:12px;margin-top:8px;">
            <button type="button" class="btn btn-outline" onclick="location.reload()">Discard Changes</button>
            <button type="submit" class="btn btn-primary"><i class="fa-solid fa-floppy-disk"></i> Save Business Profile</button>
        </div>
    </form>
</div>
'@

$before = $lines[0..($startLine - 1)]
$after = $lines[($endLine + 1)..($lines.Length - 1)]
$result = ($before + $newSection.Split("`n") + $after) -join "`r`n"
[System.IO.File]::WriteAllText((Resolve-Path $filePath), $result, [System.Text.Encoding]::UTF8)
Write-Host "Done. Replaced lines $startLine to $endLine"
