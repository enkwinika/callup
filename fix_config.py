import os

config_path = r'c:\Users\enkwi\source\repos\OnCall\Web.config'
with open(config_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

conn_string = [
    '  <connectionStrings>\n',
    '    <add name="OnCallContext" connectionString="Data Source=EUGINE-LEN\\MSSQLSERVERX;Initial Catalog=OnCallDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;" providerName="System.Data.SqlClient" />\n',
    '  </connectionStrings>\n'
]

new_lines = []
added = False
for line in lines:
    if '<appSettings>' in line and not added:
        new_lines.extend(conn_string)
        added = True
    new_lines.append(line)

with open(config_path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print("Web.config updated.")
