## Human-in-the-loop Code Interprete workflow

```mermaid
flowchart TD
user ---> | create_task | coder
user ---> | improve_code | coder
coder ---> | write_code | user
coder ---> | not_coding_task | user
user ---> | run_code | runner
runner ---> | run_code_result | assistant
assistant ---> | generate_final_reply | user
assistant ---> | fix_error | coder
coder ---> | search_solution | web
web ---> | search_solution_result | coder
```

## Get start
### Pre-requisite
- dotnet 8.0
- python with jupyter and ipykernel setup
    - to install jupyter, run `pip install jupyter`
    - to install ipykernel, run `pip install ipykernel`
    - to setup ipykernel, run `python -m ipykernel install --user --name=python3`
- env:OPENAI_API_KEY
- env:BING_API_KEY

### Run the demo
cd to the current directory and

```bash
dotnet run
```
