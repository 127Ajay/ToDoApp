using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApp.Models;

namespace TodoApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ToDoController : ControllerBase
    {
        private readonly AppDbContext _appDbContext;

        public ToDoController(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetItems()
        {
            var toDoItem = await _appDbContext.ToDoItems.ToListAsync();
            return Ok(toDoItem);
        }

        [HttpPost]
        public async Task<IActionResult> CreateItem(ToDoItem toDoItem)
        {
            if (ModelState.IsValid)
            {
                await _appDbContext.ToDoItems.AddAsync(toDoItem);
                await _appDbContext.SaveChangesAsync();

                return CreatedAtAction("GetItemById", new { toDoItem.Id }, toDoItem);
            }
            return BadRequest("Something went wrong");
        }

        [HttpGet("{Id:int}")]
        public async Task<IActionResult> GetItemById(int Id)
        {
            var item = await _appDbContext.ToDoItems.FirstOrDefaultAsync(x => x.Id == Id);

            if(item == null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        [HttpPut("{Id:int}")]
        public async Task<IActionResult> UpdateItem(int Id, ToDoItem itemToUpdate)
        {
            if(Id != itemToUpdate.Id)
            {
                return BadRequest();
            }

            var item = await _appDbContext.ToDoItems.FirstOrDefaultAsync(x => x.Id == Id);
            if (item == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                item.Title = itemToUpdate.Title;
                item.Description = itemToUpdate.Description;
                item.IsComplete = itemToUpdate.IsComplete;

                await _appDbContext.SaveChangesAsync();

                return NoContent();
            }

            return BadRequest();
        }

        [HttpDelete("{Id:int}")]
        public async Task<IActionResult> DeleteItem(int Id)
        {
            var item = await _appDbContext.ToDoItems.FirstOrDefaultAsync(x => x.Id == Id);

            if (item == null)
            {
                return NotFound();
            }
            _appDbContext.ToDoItems.Remove(item);
            await _appDbContext.SaveChangesAsync();
            return Ok(item);
        }
    }
}
