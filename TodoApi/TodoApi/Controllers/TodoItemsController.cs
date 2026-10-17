using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodoItemsController : ControllerBase
{
	private readonly TodoContext _context;

	public TodoItemsController(TodoContext context)
	{
		_context = context;
	}

	[HttpGet]
	public async Task<ActionResult<IEnumerable<TodoItemDTO>>> GetTodoItems()
	{
		var items = await _context.TodoItems
			.AsNoTracking()
			.Select(item => new TodoItemDTO
			{
				Id = item.Id,
				Name = item.Name,
				IsComplete = item.IsComplete
			})
			.ToListAsync();

		return Ok(items);
	}

	[HttpGet("{id:long}")]
	public async Task<ActionResult<TodoItemDTO>> GetTodoItem(long id)
	{
		var item = await _context.TodoItems.FindAsync(id);

		if (item is null)
		{
			return NotFound();
		}

		return Ok(ToDto(item));
	}

	[HttpPost]
	public async Task<ActionResult<TodoItemDTO>> PostTodoItem(
		TodoItemDTO dto)
	{
		var item = new TodoItem
		{
			Name = dto.Name,
			IsComplete = dto.IsComplete
		};

		_context.TodoItems.Add(item);
		await _context.SaveChangesAsync();

		return CreatedAtAction(
			nameof(GetTodoItem),
			new { id = item.Id },
			ToDto(item));
	}

	[HttpPut("{id:long}")]
	public async Task<IActionResult> PutTodoItem(
		long id,
		TodoItemDTO dto)
	{
		if (id != dto.Id)
		{
			return BadRequest(new
			{
				message = "URL-is ja päringu sisus olev ID peavad kattuma."
			});
		}

		var item = await _context.TodoItems.FindAsync(id);

		if (item is null)
		{
			return NotFound();
		}

		item.Name = dto.Name;
		item.IsComplete = dto.IsComplete;

		await _context.SaveChangesAsync();

		return NoContent();
	}

	[HttpDelete("{id:long}")]
	public async Task<IActionResult> DeleteTodoItem(long id)
	{
		var item = await _context.TodoItems.FindAsync(id);

		if (item is null)
		{
			return NotFound();
		}

		_context.TodoItems.Remove(item);
		await _context.SaveChangesAsync();

		return NoContent();
	}

	private static TodoItemDTO ToDto(TodoItem item)
	{
		return new TodoItemDTO
		{
			Id = item.Id,
			Name = item.Name,
			IsComplete = item.IsComplete
		};
	}
}